using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Notifications;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Refunds.Commands.ApproveRefund;

internal sealed class ApproveRefundCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IRefundRepository refunds,
    IShippingOrderRepository orders,
    INotificationService notifications,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<ApproveRefundCommand>
{
    public async Task Handle(ApproveRefundCommand request, CancellationToken cancellationToken)
    {
        var admin = await currentUser.LoadUserAsync(users, cancellationToken);

        var refund = await refunds.GetByIdAsync(request.RefundId, cancellationToken)
            ?? throw new NotFoundException(nameof(Refund), request.RefundId);

        var order = await orders.GetByIdAsync(refund.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), refund.OrderId);

        // DECISIONS B9, TEAM-PLAN 88: total open/paid refunds of the order (including this one) must not exceed total_fee.
        if (refund.RefundStatus == RefundStatus.Requested && refund.ProposedAmount is not null)
            order.EnsureRefundFits(0, await refunds.GetClaimedTotalForOrderAsync(order.Id, cancellationToken));

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        refund.Approve(admin.Id, clock.GetUtcNow(), note);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notifications.NotifyCustomerAsync(order, CustomerEvent.RefundDecided, cancellationToken);
    }
}

