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
    INotificationService notifications,
    IUnitOfWork unitOfWork) : ICommandHandler<RequestIncidentRefundCommand, RefundCreatedResponse>
{
    public async Task<RefundCreatedResponse> Handle(RequestIncidentRefundCommand request, CancellationToken cancellationToken)
    {
        var customer = await currentUser.LoadUserAsync(users, cancellationToken);
        var incident = await incidents.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new NotFoundException(nameof(InTransitIncident), request.IncidentId);
        var order = await orders.GetByIdAsync(incident.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), incident.OrderId);

        // ORDER_NOT_YOURS and REFUND_AMOUNT_INVALID come from the order.
        var refund = order.RequestIncidentRefund(
            customer.Id, incident.Id, request.Reason.Trim(), request.EvidenceUrls, request.RequestedAmount);
        if (await refunds.HasOpenRefundForIncidentAsync(incident.Id, cancellationToken))
            throw new ConflictException("This incident already has an open refund request.");

        refunds.Add(refund);
        await unitOfWork.SaveChangesAsync(cancellationToken); // a concurrent second request fails on RowVersion: 409

        await notifications.NotifyDispatchersAsync("INCIDENT_REFUND_REQUESTED", $"Yêu cầu hoàn tiền sự cố đơn {order.OrderCode}", refund, cancellationToken);
        return new RefundCreatedResponse(refund.Id);
    }
}
