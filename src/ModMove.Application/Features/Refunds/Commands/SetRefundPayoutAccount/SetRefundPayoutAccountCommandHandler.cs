using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Refunds.Commands.SetRefundPayoutAccount;

internal sealed class SetRefundPayoutAccountCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IRefundRepository refunds,
    IShippingOrderRepository orders,
    IMasterDataRepository masterData,
    IPaymentTransactionRepository payments,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<SetRefundPayoutAccountCommand>
{
    public async Task Handle(SetRefundPayoutAccountCommand request, CancellationToken cancellationToken)
    {
        var customer = await currentUser.LoadUserAsync(users, cancellationToken);
        var refund = await refunds.GetByIdAsync(request.RefundId, cancellationToken)
            ?? throw new NotFoundException(nameof(Refund), request.RefundId);
        var order = await orders.GetByIdAsync(refund.OrderId, cancellationToken);

        // Someone else's refund looks the same as a missing one: a 404 does not tell that it exists (like GetMyRefundDetail).
        if (order is null || order.CustomerId != customer.Id)
            throw new NotFoundException(nameof(Refund), request.RefundId);

        // A Failed refund counts for nothing in the order's total or in the "one open" checks (B9), so another request may
        // have taken its place. Bringing it back to Approved must not break those rules; checked before the refund changes.
        if (refund.RefundStatus == RefundStatus.Failed)
            await EnsureCanBeOpenAgainAsync(refund, order, cancellationToken);

        // REFUND_PAYOUT_LOCKED comes from the refund once it is Processed, Rejected or Withdrawn.
        await refund.SetPayoutAccountAsync(request.PayoutAccount, masterData, payments, clock.GetUtcNow(), cancellationToken);

        // A concurrent Administrator save moves RowVersion: 409 CONFLICT.
        // A Failed refund that returns to Approved is not pushed to the Administrator: INotificationService has no
        // Administrator channel, and the console lists Approved refunds anyway.
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureCanBeOpenAgainAsync(Refund refund, ShippingOrder order, CancellationToken cancellationToken)
    {
        order.EnsureRefundFits(refund.ProposedAmount ?? refund.RequestedAmount, await refunds.GetClaimedTotalForOrderAsync(order.Id, cancellationToken));

        if (refund.SourceType == RefundSourceType.Complaint && await refunds.HasOpenComplaintAsync(order.Id, cancellationToken))
            throw new ConflictException("This order already has an open complaint.");
        if (refund.IncidentId is { } incidentId && await refunds.HasOpenRefundForIncidentAsync(incidentId, cancellationToken))
            throw new ConflictException("This incident already has an open refund request.");
    }
}
