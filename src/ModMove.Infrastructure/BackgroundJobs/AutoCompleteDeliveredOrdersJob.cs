using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModMove.Application.Features.Orders.Commands.AutoCompleteDeliveredOrders;

namespace ModMove.Infrastructure.BackgroundJobs;

/// <summary>
/// JOB-ORDER-01 (GB-23, DECISIONS 7.3): every 15 minutes, completes Delivered orders that have reached 24 hours
/// without an open complaint and settles the driver's wallet.
/// </summary>
public sealed class AutoCompleteDeliveredOrdersJob(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<AutoCompleteDeliveredOrdersJob> logger)
    : PeriodicCommandJob(scopes, clock, logger)
{
    protected override TimeSpan Interval => TimeSpan.FromMinutes(15);

    protected override object CreateCommand() => new AutoCompleteDeliveredOrdersCommand();
}

