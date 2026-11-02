using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Tracking.Commands.PurgeOldLocationLogs;

/// <summary>JOB-GPS-01: deletes GPS points older than 30 days. Returns how many went.</summary>
public sealed record PurgeOldLocationLogsCommand : ICommand<int>;
