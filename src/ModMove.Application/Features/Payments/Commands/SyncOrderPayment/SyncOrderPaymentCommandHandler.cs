using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Features.Payments.Commands.ProcessPayOsWebhook;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Payments.Commands.SyncOrderPayment;

internal sealed class SyncOrderPaymentCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IShippingOrderRepository orders,
    IPaymentTransactionRepository payments,
    IPaymentGateway gateway,
    IDriverWalletRepository wallets,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    IDriverProfileRepository? driverProfiles = null,
    IPlatformSettings? settings = null,
    INotificationService? notifications = null,
    IRefundRepository? refunds = null) : ICommandHandler<SyncOrderPaymentCommand>
{
    /// <summary>Only the few newest codes can still be paid; older ones were replaced long ago.</summary>
    private const int CodesToAsk = 3;

    public async Task Handle(SyncOrderPaymentCommand request, CancellationToken cancellationToken)
    {
        var customer = await currentUser.LoadUserAsync(users, cancellationToken);
        var order = await orders.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), request.OrderId);
        order.EnsureCustomer(customer.Id); // ORDER_NOT_YOURS

        if (order.PaymentMethod != PaymentMethod.PayOS || order.PaymentStatus == PaymentStatus.Paid)
            return;

        var confirmation = new PayOsPaymentConfirmation(
            gateway, payments, orders, wallets, unitOfWork, clock, driverProfiles, settings, notifications, refunds);

        foreach (var transaction in (await payments.GetUnpaidPayOsForOrderAsync(order.Id, cancellationToken)).Take(CodesToAsk))
        {
            if (transaction.PayosOrderCode is not { } code)
                continue;

            var info = await gateway.GetPaymentInfoAsync(code, cancellationToken);
            // The same check as the webhook: only the exact amount settles; anything else is left for a human to look at.
            if (info is not { IsPaid: true } || info.AmountPaid != transaction.Amount)
                continue;

            await confirmation.ConfirmPaidAsync(
                transaction, new PayOsPayer(info.PayerBankBin, info.PayerAccountNumber, info.PayerAccountName), cancellationToken);
            return;
        }
    }
}
