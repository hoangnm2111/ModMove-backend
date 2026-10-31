using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Orders.Commands.ReviewPickupEvidence;

internal sealed class ReviewPickupEvidenceCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IShippingOrderRepository orders,
    IDriverProfileRepository driverProfiles,
    INotificationService notifications,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<ReviewPickupEvidenceCommand>
{
    public async Task Handle(ReviewPickupEvidenceCommand request, CancellationToken cancellationToken)
    {
        var customer = await currentUser.LoadUserAsync(users, cancellationToken);
        var order = await orders.GetWithProofAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), request.OrderId);

        // ORDER_NOT_YOURS and "nothing waiting for review" come from the entity.
        var reason = request.Reason?.Trim();
        if (request.Approve)
            order.ApprovePickupEvidence(customer.Id, clock.GetUtcNow());
        else
            order.RejectPickupEvidence(reason!, customer.Id);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var driverUserId = await driverProfiles.DriverUserIdAsync(order, cancellationToken);
        if (request.Approve)
        {
            await notifications.PublishOrderStatusAsync(order, driverUserId, cancellationToken); // now InTransit
            await notifications.NotifyDriverAsync(driverUserId, order, "PICKUP_APPROVED",
                "Khách đã duyệt ảnh lấy hàng", $"Bắt đầu vận chuyển đơn {order.OrderCode}.", cancellationToken);
        }
        else
        {
            await notifications.NotifyDriverAsync(driverUserId, order, "PICKUP_REJECTED",
                "Khách từ chối ảnh lấy hàng", $"Chụp lại ảnh hàng của đơn {order.OrderCode}: {reason}", cancellationToken,
                ("reason", reason!));
        }
    }
}
