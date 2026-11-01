using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Incidents.Commands.StartIncidentHandling;

internal sealed class StartIncidentHandlingCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IRepository<InTransitIncident> incidents,
    IUnitOfWork unitOfWork) : ICommandHandler<StartIncidentHandlingCommand>
{
    public async Task Handle(StartIncidentHandlingCommand request, CancellationToken cancellationToken)
    {
        var dispatcher = await currentUser.LoadUserAsync(users, cancellationToken);
        var incident = await incidents.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new NotFoundException(nameof(InTransitIncident), request.IncidentId);

        IncidentHandlingRules.EnsureCanHandle(incident, dispatcher.Id);
        incident.StartHandling(dispatcher.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

