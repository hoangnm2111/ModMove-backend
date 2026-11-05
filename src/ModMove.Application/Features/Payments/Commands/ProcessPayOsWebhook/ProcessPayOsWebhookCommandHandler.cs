using System.Text.Json;
using FluentValidation.Results;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Notifications;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.Application.Features.Payments.Commands.ProcessPayOsWebhook;

internal sealed class ProcessPayOsWebhookCommandHandler(
    IPaymentGateway gateway,
    IPaymentTransactionRepository payments,
    IShippingOrderRepository orders,
    IDriverWalletRepository wallets,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    IDriverProfileRepository? driverProfiles = null,
    IPlatformSettings? settings = null,
    INotificationService? notifications = null) : ICommandHandler<ProcessPayOsWebhookCommand>
{
    private const string SuccessCode = "00";

    public async Task Handle(ProcessPayOsWebhookCommand request, CancellationToken cancellationToken)
    {
        var signature = ExtractSignature(request.RawBody);
        if (!gateway.VerifyWebhookSignature(request.RawBody, signature))
            throw new UnauthorizedException("Invalid PayOS webhook signature.");

        var (payosOrderCode, amount, isSuccess) = ParsePayload(request.RawBody);

        var transaction = await payments.GetByPayOsOrderCodeAsync(payosOrderCode, cancellationToken)
            ?? throw new NotFoundException(nameof(PaymentTransaction), payosOrderCode);

        if (amount != transaction.Amount)
            throw new DomainException(
                $"The paid amount ({amount:0} VND) does not match the transaction amount ({transaction.Amount:0} VND).",
                "PAYMENT_AMOUNT_MISMATCH");

        if (!isSuccess)
        {
            if (transaction.TransactionStatus == PaymentTransactionStatus.Pending)
            {
                transaction.MarkFailed();
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        // Idempotent: a duplicate webhook for an already-Paid session (or a Cancelled/Expired one) is a no-op.
        var now = clock.GetUtcNow();
        if (!transaction.MarkPaid(now))
            return;

        ShippingOrder? paidOrder = null;
        if (transaction.OrderId is { } orderId)
        {
            var order = await orders.GetByIdAsync(orderId, cancellationToken)
                ?? throw new NotFoundException(nameof(ShippingOrder), orderId);

            // Throws ORDER_CANCELLED if the order was cancelled before the payment arrived.
            order.MarkPaid(now);
            paidOrder = order;

            foreach (var other in await payments.GetPendingPayOsForOrderAsync(order.Id, cancellationToken))
            {
                if (other.Id != transaction.Id)
                    other.Cancel();
            }
        }
        else if (transaction.WalletId is { } walletId)
        {
            var wallet = await wallets.GetByIdAsync(walletId, cancellationToken)
                ?? throw new NotFoundException(nameof(DriverWallet), walletId);

            wallet.TopUpDeposit(transaction.Amount);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // DECISIONS 6.4, 8.3: PayOS orders enter the matching pool only once PaidAt is set.
        // Notify eligible nearby online drivers immediately via FCM + SignalR.
        if (paidOrder is not null
            && notifications is not null
            && driverProfiles is not null
            && settings is not null)
        {
            await notifications.NotifyNearbyDriversOfNewOrderAsync(
                paidOrder, driverProfiles, wallets, orders, settings, now, cancellationToken);
        }
    }

    private static string ExtractSignature(string rawBody)
    {
        try
        {
            using var json = JsonDocument.Parse(rawBody);
            return json.RootElement.ValueKind == JsonValueKind.Object
                && json.RootElement.TryGetProperty("signature", out var sig)
                && sig.ValueKind == JsonValueKind.String
                ? sig.GetString() ?? string.Empty
                : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static (long OrderCode, decimal Amount, bool IsSuccess) ParsePayload(string rawBody)
    {
        using var json = JsonDocument.Parse(rawBody);
        var root = json.RootElement;

        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Object)
        {
            throw new ValidationException([new ValidationFailure("data", "PayOS webhook data object is required.")]);
        }

        var failures = new List<ValidationFailure>();

        long orderCode = 0;
        if (!data.TryGetProperty("orderCode", out var codeProp)
            || !codeProp.TryGetInt64(out orderCode)
            || orderCode <= 0)
        {
            failures.Add(new ValidationFailure("data.orderCode", "A positive PayOS orderCode is required."));
        }

        decimal amount = 0;
        if (!data.TryGetProperty("amount", out var amountProp)
            || !amountProp.TryGetDecimal(out amount)
            || amount <= 0)
        {
            failures.Add(new ValidationFailure("data.amount", "A positive amount is required."));
        }

        if (failures.Count > 0)
            throw new ValidationException(failures);

        var rootCodeOk = !root.TryGetProperty("code", out var rc)
            || (rc.ValueKind == JsonValueKind.String && rc.GetString() == SuccessCode);
        var rootSuccessOk = !root.TryGetProperty("success", out var rs)
            || rs.ValueKind == JsonValueKind.True;
        var dataCodeOk = !data.TryGetProperty("code", out var dc)
            || (dc.ValueKind == JsonValueKind.String && dc.GetString() == SuccessCode);

        return (orderCode, amount, rootCodeOk && rootSuccessOk && dataCodeOk);
    }
}

