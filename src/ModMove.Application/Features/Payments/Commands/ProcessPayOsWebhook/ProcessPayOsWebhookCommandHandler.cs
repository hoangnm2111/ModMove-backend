using System.Text.Json;
using FluentValidation.Results;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Notifications;
using ModMove.Domain.Constants;
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
    INotificationService? notifications = null,
    IRefundRepository? refunds = null) : ICommandHandler<ProcessPayOsWebhookCommand>
{
    private const string SuccessCode = "00";

    public async Task Handle(ProcessPayOsWebhookCommand request, CancellationToken cancellationToken)
    {
        var signature = ExtractSignature(request.RawBody);
        if (!gateway.VerifyWebhookSignature(request.RawBody, signature))
            throw new UnauthorizedException("Invalid PayOS webhook signature.");

        var (payosOrderCode, amount, isSuccess) = ParsePayload(request.RawBody);

        var transaction = await payments.GetByPayOsOrderCodeAsync(payosOrderCode, cancellationToken);
        if (transaction is null)
        {
            // When registering the webhook URL via confirm-webhook, PayOS sends a signed probe with orderCode = 123.
            if (payosOrderCode == BusinessRules.PayOsProbeOrderCode)
                return;

            throw new NotFoundException(nameof(PaymentTransaction), payosOrderCode);
        }

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

        // Idempotent: a duplicate webhook for an already-Paid session is a no-op.
        // Late transfers arriving after a session was locally marked Expired or Cancelled still transition to Paid.
        var now = clock.GetUtcNow();
        if (!transaction.MarkPaid(now))
            return;

        ShippingOrder? paidOrder = null;
        ShippingOrder? refundOrder = null;
        if (transaction.OrderId is { } orderId)
        {
            var order = await orders.GetByIdAsync(orderId, cancellationToken)
                ?? throw new NotFoundException(nameof(ShippingOrder), orderId);

            foreach (var other in await payments.GetPendingPayOsForOrderAsync(order.Id, cancellationToken))
            {
                if (other.Id == transaction.Id)
                    continue;

                other.Cancel();
                if (other.PayosOrderCode is { } otherCode && other.ExpiresAt > now)
                    await gateway.CancelPaymentLinkAsync(otherCode, cancellationToken);
            }

            if (order.OrderStatus == OrderStatus.Cancelled)
            {
                // Customer or JOB-PAY-01 cancelled the order before the bank transfer arrived:
                // record the payment and queue a 100% cancellation refund for Admin approval instead of failing with 422.
                var reason = !string.IsNullOrWhiteSpace(order.CancellationReason)
                    ? $"Thanh toán đến sau khi đơn đã hủy: {order.CancellationReason}"
                    : "Thanh toán đến sau khi đơn đã hủy.";
                refunds?.Add(Refund.Request(
                    order.Id,
                    incidentId: null,
                    RefundSourceType.Cancellation,
                    reason,
                    evidenceUrls: null,
                    transaction.Amount,
                    order.CancellationReasonCode));
                refundOrder = order;
            }
            else if (order.PaymentStatus == PaymentStatus.Paid)
            {
                // Customer paid twice across two different QR sessions for the same order: queue a 100% refund for the extra payment.
                refunds?.Add(Refund.Request(
                    order.Id,
                    incidentId: null,
                    RefundSourceType.Cancellation,
                    "Khách chuyển khoản trùng lần 2 cho cùng đơn hàng.",
                    evidenceUrls: null,
                    transaction.Amount,
                    order.CancellationReasonCode));
                refundOrder = order;
            }
            else
            {
                order.MarkPaid(now);
                paidOrder = order;
            }
        }
        else if (transaction.WalletId is { } walletId)
        {
            var wallet = await wallets.GetByIdAsync(walletId, cancellationToken)
                ?? throw new NotFoundException(nameof(DriverWallet), walletId);

            // The money is already received: credit it, then keep GB-32 (a deposit still short is refilled from earnings).
            wallet.CreditTopUp(transaction.Amount);
            if (wallet.EarningsBalance > 0 && settings is not null)
                wallet.RefillDepositFromEarnings(await settings.GetDecimalAsync(SettingKeys.RequiredDeposit, cancellationToken));

            foreach (var other in await payments.GetPendingPayOsForWalletAsync(wallet.Id, cancellationToken))
            {
                if (other.Id == transaction.Id)
                    continue;

                other.Cancel();
                if (other.PayosOrderCode is { } otherCode && other.ExpiresAt > now)
                    await gateway.CancelPaymentLinkAsync(otherCode, cancellationToken);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // R-01: a late or double payment is owed back; the money has nowhere to go until the customer gives a bank account.
        if (refundOrder is not null && refunds is not null && notifications is not null)
            await notifications.NotifyCustomerAsync(refundOrder, CustomerEvent.RefundBankInfoRequired, cancellationToken);

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

