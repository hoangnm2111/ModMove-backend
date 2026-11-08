using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Notifications;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Payments.Commands.ExpireStalePayments;

internal sealed class ExpireStalePaymentsCommandHandler(
    IPaymentTransactionRepository payments,
    IShippingOrderRepository orders,
    INotificationService notifications,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    IPaymentGateway? gateway = null) : ICommandHandler<ExpireStalePaymentsCommand, int>
{
    public const string TimeoutReason = "Đơn hàng bị hủy do quá thời hạn thanh toán (30 phút).";

    public async Task<int> Handle(ExpireStalePaymentsCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();

        var pendingTransactions = await payments.GetPendingPayOsTransactionsAsync(cancellationToken);
        var expiredCount = 0;
        foreach (var transaction in pendingTransactions)
        {
            if (!transaction.ExpireIfDue(now))
                continue;

            payments.Update(transaction);
            expiredCount++;
        }

        var pendingOrders = await orders.GetPendingAsync(cancellationToken);
        var cancelledOrders = new List<ShippingOrder>();
        foreach (var order in pendingOrders)
        {
            if (!order.IsPaymentExpired(now))
                continue;

            order.Cancel(TimeoutReason, ChangedByRole.System, userId: null, now);
            orders.Update(order);
            cancelledOrders.Add(order);

            // Cancel any PayOS session created shortly before the 30-minute deadline (e.g. at minute 29)
            // so the QR code does not remain live at PayOS after the order itself has been cancelled.
            foreach (var pendingTx in pendingTransactions)
            {
                if (pendingTx.OrderId != order.Id || pendingTx.TransactionStatus != PaymentTransactionStatus.Pending)
                    continue;

                pendingTx.Cancel();
                payments.Update(pendingTx);
                if (gateway is not null && pendingTx.PayosOrderCode is { } code && pendingTx.ExpiresAt > now)
                    await gateway.CancelPaymentLinkAsync(code, cancellationToken);
            }
        }

        var totalChanged = expiredCount + cancelledOrders.Count;
        if (totalChanged == 0)
            return 0;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var order in cancelledOrders)
        {
            await notifications.PublishOrderStatusAsync(order, driverUserId: null, cancellationToken);
            await notifications.NotifyCustomerAsync(order, CustomerEvent.OrderCancelled, cancellationToken, TimeoutReason);
        }

        return totalChanged;
    }
}

