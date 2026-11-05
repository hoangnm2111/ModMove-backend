using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Notifications;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Orders.Commands.AutoCompleteDeliveredOrders;

internal sealed class AutoCompleteDeliveredOrdersCommandHandler(
    IShippingOrderRepository orders,
    IRefundRepository refunds,
    IDriverProfileRepository driverProfiles,
    OrderSettlement settlement,
    INotificationService notifications,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<AutoCompleteDeliveredOrdersCommand, int>
{
    public async Task<int> Handle(AutoCompleteDeliveredOrdersCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var deliveredOrders = await orders.GetDeliveredOrdersAsync(cancellationToken);
        if (deliveredOrders.Count == 0)
            return 0;

        decimal? commissionRate = null;
        var completedOrders = new List<ShippingOrder>();

        foreach (var order in deliveredOrders)
        {
            if (!order.CanBeAutoCompleted(now))
                continue;

            // DECISIONS 7.3: an open complaint holds the order; it completes once the complaint is closed.
            if (await refunds.HasOpenComplaintAsync(order.Id, cancellationToken))
                continue;

            commissionRate ??= await settlement.CommissionRateAsync(cancellationToken);

            order.Complete(commissionRate.Value, ChangedByRole.System, userId: null, now);
            await settlement.SettleAsync(order, cancellationToken);
            orders.Update(order);
            completedOrders.Add(order);
        }

        if (completedOrders.Count == 0)
            return 0;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var order in completedOrders)
        {
            var driverUserId = await driverProfiles.DriverUserIdAsync(order, cancellationToken);
            await notifications.PublishOrderStatusAsync(order, driverUserId, cancellationToken);
            await notifications.NotifyCustomerAsync(order, CustomerEvent.OrderCompleted, cancellationToken);
            await notifications.NotifyDriverAsync(driverUserId, order, "ORDER_COMPLETED",
                "Đơn đã hoàn thành", $"Đơn {order.OrderCode} đã tự động hoàn tất sau 24h và tất toán ví.", cancellationToken);
        }

        return completedOrders.Count;
    }
}

