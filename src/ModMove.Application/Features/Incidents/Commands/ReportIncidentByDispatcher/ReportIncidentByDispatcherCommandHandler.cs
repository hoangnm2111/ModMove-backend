using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Features.Incidents.Commands.ReportIncidentByCustomer;
using ModMove.Application.Features.Orders;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Incidents.Commands.ReportIncidentByDispatcher;

internal sealed class ReportIncidentByDispatcherCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IShippingOrderRepository orders,
    IRepository<InTransitIncident> incidents,
    IDriverProfileRepository driverProfiles,
    INotificationService notifications,
    IUnitOfWork unitOfWork) : ICommandHandler<ReportIncidentByDispatcherCommand, IncidentCreatedResponse>
{
    public async Task<IncidentCreatedResponse> Handle(ReportIncidentByDispatcherCommand request, CancellationToken cancellationToken)
    {
        var dispatcher = await currentUser.LoadUserAsync(users, cancellationToken);
        var order = await orders.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), request.OrderId);

        var incident = order.ReportIncidentByDispatcher(dispatcher.Id, request.IncidentType, request.Description.Trim()); // INCIDENT_NOT_ALLOWED
        incidents.Add(incident);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // 8.3: the same generic notice as a Dispatcher taking over any incident; the note itself stays internal.
        var driverUserId = await driverProfiles.DriverUserIdAsync(order, cancellationToken);
        await IncidentNotifications.NotifyHandlingStartedAsync(notifications, order, driverUserId, incident, cancellationToken);
        return new IncidentCreatedResponse(incident.Id);
    }
}
