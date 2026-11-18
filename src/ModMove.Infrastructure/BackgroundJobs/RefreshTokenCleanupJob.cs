using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModMove.Application.Features.Auth.Commands.PurgeExpiredRefreshTokens;

namespace ModMove.Infrastructure.BackgroundJobs;

/// <summary>JOB-AUTH-01: once a day, deletes expired refresh tokens (each refresh adds a row, 9.1).</summary>
public sealed class RefreshTokenCleanupJob(IServiceScopeFactory scopes, TimeProvider clock, ILogger<RefreshTokenCleanupJob> logger)
    : PeriodicCommandJob(scopes, clock, logger)
{
    protected override TimeSpan Interval => TimeSpan.FromHours(24);

    protected override object CreateCommand() => new PurgeExpiredRefreshTokensCommand();
}
