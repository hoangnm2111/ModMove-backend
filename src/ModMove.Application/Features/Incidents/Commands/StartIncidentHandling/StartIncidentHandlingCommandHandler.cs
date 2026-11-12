using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Incidents.Commands.StartIncidentHandling;

internal sealed class StartIncidentHandlingCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IRepository<InTransitIncident> incidents,
    IShippingOrderRepository orders,
    IDriverProfileRepository driverProfiles,
    INotificationService notifications,
    IUnitOfWork unitOfWork) : ICommandHandler<StartIncidentHandlingCommand>
{
    public async Task Handle(StartIncidentHandlingCommand request, CancellationToken cancellationToken)
    {
        var dispatcher = await currentUser.LoadUserAsync(users, cancellationToken);
        var incident = await incidents.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new NotFoundException(nameof(InTransitIncident), request.IncidentId);

        IncidentHandlingRules.EnsureCanHandle(incident, dispatcher.Id);
        var wasOpen = incident.ResolutionStatus == IncidentResolutionStatus.Open;
        incident.StartHandling(dispatcher.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // 8.3: only the move Open -> InProgress is news; the same Dispatcher pressing start again says nothing.
        if (!wasOpen)
            return;

        var order = await orders.GetByIdAsync(incident.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), incident.OrderId);
        var driverUserId = await IncidentNotifications.DriverConcernedAsync(incident, order, users, driverProfiles, cancellationToken);
        await IncidentNotifications.NotifyHandlingStartedAsync(notifications, order, driverUserId, incident, cancellationToken);
    }
}
