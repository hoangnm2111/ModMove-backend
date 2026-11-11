using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Notifications;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Matching.Commands.CancelUnmatchedOrders;

internal sealed class CancelUnmatchedOrdersCommandHandler(
    IShippingOrderRepository orders,
    IRefundRepository refunds,
    INotificationService notifications,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<CancelUnmatchedOrdersCommand, int>
{
    public const string TimeoutReason = "Không tìm được tài xế nhận đơn trong 10 phút.";

    public async Task<int> Handle(CancelUnmatchedOrdersCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var pendingOrders = await orders.GetPendingAsync(cancellationToken);

        var cancelledOrders = new List<ShippingOrder>();
        var owingRefund = new HashSet<ShippingOrder>();
        foreach (var order in pendingOrders)
        {
            if (!order.IsMatchingExpired(now))
                continue;

            order.Cancel(TimeoutReason, ChangedByRole.System, userId: null, now);
            orders.Update(order);

            // GB-41, DECISIONS B9, 3.3: Paid PayOS orders cancelled on matching timeout get a 100% Cancellation refund
            // that goes straight to Admin (ProposedAmount = RequestedAmount = TotalFee).
            if (order.PaymentMethod == PaymentMethod.PayOS && order.PaymentStatus == PaymentStatus.Paid && order.TotalFee > 0)
            {
                refunds.Add(Refund.Request(
                    order.Id,
                    incidentId: null,
                    RefundSourceType.Cancellation,
                    TimeoutReason,
                    evidenceUrls: null,
                    order.TotalFee));
                owingRefund.Add(order);
            }

            cancelledOrders.Add(order);
        }

        if (cancelledOrders.Count == 0)
            return 0;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var order in cancelledOrders)
        {
            await notifications.PublishOrderStatusAsync(order, driverUserId: null, cancellationToken);
            await notifications.NotifyCustomerAsync(order, CustomerEvent.OrderCancelled, cancellationToken, TimeoutReason);

            // R-01: the refund is owed; the money has nowhere to go until the customer gives a bank account.
            if (owingRefund.Contains(order))
                await notifications.NotifyCustomerAsync(order, CustomerEvent.RefundBankInfoRequired, cancellationToken);
        }

        return cancelledOrders.Count;
    }
}

