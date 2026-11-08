using Microsoft.Extensions.Hosting;

namespace ModMove.IntegrationTests;

/// <summary>Starts a job, waits for what it should do, and stops it.</summary>
internal static class HostedJobRunner
{
    // Only a failure ever waits this long; a slow CI machine must not turn into a red build.
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Starts outside xUnit's synchronization context. PeriodicCommandJob's Task.Yield would otherwise queue the job on the
    /// few threads xUnit runs tests on, where other tests can hold it back past any short wait. A host has no such context.
    /// </summary>
    public static async Task RunUntilAsync(IHostedService job, Task reached)
    {
        await Task.Run(() => job.StartAsync(CancellationToken.None));
        try
        {
            await reached.WaitAsync(Timeout);
        }
        finally
        {
            await job.StopAsync(CancellationToken.None);
        }
    }
}
