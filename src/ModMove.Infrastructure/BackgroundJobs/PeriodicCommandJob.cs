using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ModMove.Infrastructure.BackgroundJobs;

/// <summary>
/// Sends one Command at startup and then on every <see cref="Interval"/> (TEAM-PLAN 2: a job only sends a Command).
/// Each run has its own scope, and a failed run is logged so the next one still happens.
/// There is no signed-in user here: a Command that records who changed something uses ChangedByRole.System.
/// </summary>
public abstract class PeriodicCommandJob(IServiceScopeFactory scopes, TimeProvider clock, ILogger logger) : BackgroundService
{
    protected abstract TimeSpan Interval { get; }

    protected abstract object CreateCommand();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield(); // do not hold up the host while it starts

        try
        {
            using var timer = new PeriodicTimer(Interval, clock);
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(CreateCommand(), stoppingToken);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Background job {Job} failed; it runs again in {Interval}", GetType().Name, Interval);
        }
    }
}
