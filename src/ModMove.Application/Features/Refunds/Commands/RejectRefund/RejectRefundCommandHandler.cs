using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Notifications;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Refunds.Commands.RejectRefund;

internal sealed class RejectRefundCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IRefundRepository refunds,
    IShippingOrderRepository orders,
    INotificationService notifications,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<RejectRefundCommand>
{
    public async Task Handle(RejectRefundCommand request, CancellationToken cancellationToken)
    {
        var admin = await currentUser.LoadUserAsync(users, cancellationToken);

        var refund = await refunds.GetByIdAsync(request.RefundId, cancellationToken)
            ?? throw new NotFoundException(nameof(Refund), request.RefundId);

        var order = await orders.GetByIdAsync(refund.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), refund.OrderId);

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        refund.RejectRefund(admin.Id, clock.GetUtcNow(), note);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notifications.NotifyCustomerAsync(order, CustomerEvent.RefundDecided, cancellationToken);
    }
}

