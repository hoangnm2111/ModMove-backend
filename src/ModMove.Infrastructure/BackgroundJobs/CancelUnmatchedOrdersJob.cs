using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModMove.Application.Features.Matching.Commands.CancelUnmatchedOrders;

namespace ModMove.Infrastructure.BackgroundJobs;

/// <summary>
/// JOB-MATCH-01 (GB-24, DECISIONS 3.2): every minute, cancels Pending orders that have waited 10 minutes without a driver.
/// </summary>
public sealed class CancelUnmatchedOrdersJob(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<CancelUnmatchedOrdersJob> logger)
    : PeriodicCommandJob(scopes, clock, logger)
{
    protected override TimeSpan Interval => TimeSpan.FromMinutes(1);

    protected override object CreateCommand() => new CancelUnmatchedOrdersCommand();
}

