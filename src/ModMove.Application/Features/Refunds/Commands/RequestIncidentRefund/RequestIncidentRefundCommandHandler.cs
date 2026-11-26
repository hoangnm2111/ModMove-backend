using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Refunds.Commands.RequestIncidentRefund;

internal sealed class RequestIncidentRefundCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IRepository<InTransitIncident> incidents,
    IShippingOrderRepository orders,
    IRefundRepository refunds,
    IMasterDataRepository masterData,
    IPaymentTransactionRepository payments,
    INotificationService notifications,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<RequestIncidentRefundCommand, RefundCreatedResponse>
{
    public async Task<RefundCreatedResponse> Handle(RequestIncidentRefundCommand request, CancellationToken cancellationToken)
    {
        var customer = await currentUser.LoadUserAsync(users, cancellationToken);
        var incident = await incidents.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new NotFoundException(nameof(InTransitIncident), request.IncidentId);
        var order = await orders.GetByIdAsync(incident.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), incident.OrderId);

        // ORDER_NOT_YOURS, REFUND_AMOUNT_INVALID, REFUND_NOT_ALLOWED and REFUND_TOTAL_EXCEEDED come from the order.
        var refund = order.RequestIncidentRefund(
            customer.Id, incident.Id, request.Reason.Trim(),
            request.EvidenceUrls?.Select(storage.ToStoredUrl).ToList(), request.RequestedAmount);
        if (await refunds.HasOpenRefundForIncidentAsync(incident.Id, cancellationToken))
            throw new ConflictException("This incident already has an open refund request.");
        order.EnsureRefundFits(request.RequestedAmount, await refunds.GetClaimedTotalForOrderAsync(order.Id, cancellationToken));

        await refund.SetPayoutAccountAsync(request.PayoutAccount, masterData, payments, clock.GetUtcNow(), cancellationToken); // R-01

        refunds.Add(refund);
        await unitOfWork.SaveChangesAsync(cancellationToken); // a concurrent second request fails on RowVersion: 409

        await notifications.NotifyDispatchersAsync("INCIDENT_REFUND_REQUESTED", $"Yêu cầu hoàn tiền sự cố đơn {order.OrderCode}", refund, cancellationToken);
        return new RefundCreatedResponse(refund.Id);
    }
}
