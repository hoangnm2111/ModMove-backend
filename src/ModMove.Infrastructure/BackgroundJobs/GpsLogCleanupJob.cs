using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModMove.Application.Features.Tracking.Commands.PurgeOldLocationLogs;

namespace ModMove.Infrastructure.BackgroundJobs;

/// <summary>JOB-GPS-01: once a day, deletes GPS points older than 30 days.</summary>
public sealed class GpsLogCleanupJob(IServiceScopeFactory scopes, TimeProvider clock, ILogger<GpsLogCleanupJob> logger)
    : PeriodicCommandJob(scopes, clock, logger)
{
    protected override TimeSpan Interval => TimeSpan.FromHours(24);

    protected override object CreateCommand() => new PurgeOldLocationLogsCommand();
}
