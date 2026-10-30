using System.Globalization;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Orders.Commands.CancelOrder;

internal sealed class CancelOrderCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IShippingOrderRepository orders,
    IRefundRepository refunds,
    IDriverProfileRepository driverProfiles,
    INotificationService notifications,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<CancelOrderCommand>
{
    public async Task Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var customer = await currentUser.LoadUserAsync(users, cancellationToken);
        var order = await orders.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), request.OrderId);

        var reason = request.Reason.Trim();
        // ORDER_NOT_YOURS / ORDER_NOT_CANCELLABLE come from the entity. A coupon use is not given back (B11).
        var refundAmount = order.CancelByCustomer(customer.Id, reason, clock.GetUtcNow());
        if (refundAmount > 0)
            refunds.Add(Refund.Request(order.Id, null, RefundSourceType.Cancellation, reason, null, refundAmount));

        // Accept, MarkPaid and Cancel all bump RowVersion: of two racing saves, the later one is a 409.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (order.DriverProfileId is { } driverProfileId
            && await driverProfiles.GetByIdAsync(driverProfileId, cancellationToken) is { } driver)
        {
            await notifications.NotifyAsync(driver.UserId, new Notification(
                "ORDER_CANCELLED", "Đơn đã bị hủy", $"Khách đã hủy đơn {order.OrderCode}.",
                new Dictionary<string, string> { ["orderId"] = order.Id.ToString(CultureInfo.InvariantCulture) }),
                cancellationToken);
        }
    }
}
