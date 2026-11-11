using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Notifications;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Refunds.Commands.MarkRefundPayoutFailed;

internal sealed class MarkRefundPayoutFailedCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IRefundRepository refunds,
    IShippingOrderRepository orders,
    INotificationService notifications,
    IUnitOfWork unitOfWork) : ICommandHandler<MarkRefundPayoutFailedCommand>
{
    public async Task Handle(MarkRefundPayoutFailedCommand request, CancellationToken cancellationToken)
    {
        await currentUser.LoadUserAsync(users, cancellationToken);

        var refund = await refunds.GetByIdAsync(request.RefundId, cancellationToken)
            ?? throw new NotFoundException(nameof(Refund), request.RefundId);

        var order = await orders.GetByIdAsync(refund.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), refund.OrderId);

        refund.MarkFailed(request.Reason.Trim());

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // R-01: CustomerNotifications.RefundPayoutFailed keeps the account number and internal notes out of notifications.
        await notifications.NotifyCustomerAsync(order, CustomerEvent.RefundPayoutFailed, cancellationToken);
    }
}

