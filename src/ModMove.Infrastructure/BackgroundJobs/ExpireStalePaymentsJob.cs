using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModMove.Application.Features.Payments.Commands.ExpireStalePayments;

namespace ModMove.Infrastructure.BackgroundJobs;

/// <summary>
/// JOB-PAY-01 (GB-13, DECISIONS 3.1): every minute, expires Pending PayOS sessions after 10 minutes
/// and cancels Unpaid PayOS orders still Pending 30 minutes after booking.
/// </summary>
public sealed class ExpireStalePaymentsJob(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<ExpireStalePaymentsJob> logger)
    : PeriodicCommandJob(scopes, clock, logger)
{
    protected override TimeSpan Interval => TimeSpan.FromMinutes(1);

    protected override object CreateCommand() => new ExpireStalePaymentsCommand();
}

