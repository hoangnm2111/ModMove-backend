using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Incidents.Commands.ReleaseIncident;

internal sealed class ReleaseIncidentCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IRepository<InTransitIncident> incidents,
    INotificationService notifications,
    IUnitOfWork unitOfWork) : ICommandHandler<ReleaseIncidentCommand>
{
    public async Task Handle(ReleaseIncidentCommand request, CancellationToken cancellationToken)
    {
        var dispatcher = await currentUser.LoadUserAsync(users, cancellationToken);
        var incident = await incidents.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new NotFoundException(nameof(InTransitIncident), request.IncidentId);

        IncidentHandlingRules.EnsureCanHandle(incident, dispatcher.Id); // 409 while another Dispatcher holds it
        incident.Release(dispatcher.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await IncidentNotifications.NotifyReleasedAsync(notifications, incident, cancellationToken);
    }
}
