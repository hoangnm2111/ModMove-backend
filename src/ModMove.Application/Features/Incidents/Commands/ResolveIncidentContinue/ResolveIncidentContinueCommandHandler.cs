using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

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

        // With Proof: the pickup photo count decides whether the trip can still move on (DECISIONS 6.3).
        var order = await orders.GetWithProofAsync(incident.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), incident.OrderId);

        order.EnsureTripCanContinue(); // ORDER_NOT_ACTIVE, PICKUP_PHOTO_LIMIT

        var reason = request.Reason.Trim();
        incident.Resolve(IncidentDecision.ContinueTrip, reason, dispatcher.Id, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var driverUserId = await IncidentNotifications.DriverConcernedAsync(incident, order, users, driverProfiles, cancellationToken);
        await IncidentNotifications.NotifyContinueTripAsync(notifications, order, driverUserId, incident, reason, cancellationToken);
    }
}

