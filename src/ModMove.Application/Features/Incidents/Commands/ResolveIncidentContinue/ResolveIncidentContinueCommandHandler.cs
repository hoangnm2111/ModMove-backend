using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Features.Orders;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.Application.Features.Incidents.Commands.ResolveIncidentContinue;

internal sealed class ResolveIncidentContinueCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IRepository<InTransitIncident> incidents,
    IShippingOrderRepository orders,
    IDriverProfileRepository driverProfiles,
    INotificationService notifications,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<ResolveIncidentContinueCommand>
{
    public async Task Handle(ResolveIncidentContinueCommand request, CancellationToken cancellationToken)
    {
        var dispatcher = await currentUser.LoadUserAsync(users, cancellationToken);
        var incident = await incidents.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new NotFoundException(nameof(InTransitIncident), request.IncidentId);

        IncidentHandlingRules.EnsureCanHandle(incident, dispatcher.Id);

        var order = await orders.GetByIdAsync(incident.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), incident.OrderId);

        if (order.OrderStatus is not (OrderStatus.Accepted or OrderStatus.Arrived or OrderStatus.PickedUp or OrderStatus.InTransit))
            throw new DomainException("The trip can only continue while the order is active.", "ORDER_NOT_ACTIVE");

        var reason = request.Reason.Trim();
        incident.Resolve(IncidentDecision.ContinueTrip, reason, dispatcher.Id, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var driverUserId = await driverProfiles.DriverUserIdAsync(order, cancellationToken);
        await IncidentNotifications.NotifyContinueTripAsync(notifications, order, driverUserId, incident, reason, cancellationToken);
    }
}

