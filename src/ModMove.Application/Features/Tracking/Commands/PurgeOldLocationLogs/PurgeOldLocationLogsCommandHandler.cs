using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Constants;

namespace ModMove.Application.Features.Tracking.Commands.PurgeOldLocationLogs;

/// <summary>No IUnitOfWork: the repository deletes in bulk straight away instead of loading every old point.</summary>
internal sealed class PurgeOldLocationLogsCommandHandler(IDriverLocationLogRepository locationLogs, TimeProvider clock)
    : ICommandHandler<PurgeOldLocationLogsCommand, int>
{
    public Task<int> Handle(PurgeOldLocationLogsCommand request, CancellationToken cancellationToken) =>
        locationLogs.DeleteRecordedBeforeAsync(clock.GetUtcNow() - BusinessRules.GpsLogRetention, cancellationToken);
}
