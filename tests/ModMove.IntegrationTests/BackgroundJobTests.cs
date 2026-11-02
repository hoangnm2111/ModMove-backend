using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ModMove.Application.Features.Tracking.Commands.PurgeOldLocationLogs;
using ModMove.Infrastructure.BackgroundJobs;

namespace ModMove.IntegrationTests;

/// <summary>JOB-GPS-01 and the base class the other jobs share: each run sends one command through its own scope.</summary>
public class BackgroundJobTests
{
    private sealed class RecordingSender : ISender
    {
        private readonly TaskCompletionSource _enough = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<object> Sent { get; } = [];
        public int FailFirst { get; init; }
        public int Enough { get; init; } = 1;
        public Task Reached => _enough.Task;

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            lock (Sent)
            {
                Sent.Add(request);
                if (Sent.Count >= Enough)
                    _enough.TrySetResult();
                if (Sent.Count <= FailFirst)
                    throw new InvalidOperationException("The database is down");
            }

            return Task.FromResult<object?>(0);
        }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed record TestCommand;

    private sealed class FastJob(IServiceScopeFactory scopes, TimeProvider clock)
        : PeriodicCommandJob(scopes, clock, NullLogger<FastJob>.Instance)
    {
        protected override TimeSpan Interval => TimeSpan.FromMilliseconds(10);
        protected override object CreateCommand() => new TestCommand();
    }

    private static IServiceScopeFactory Scopes(ISender sender) =>
        new ServiceCollection().AddSingleton(sender).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

    private static async Task RunUntilReachedAsync(IHostedService job, RecordingSender sender)
    {
        await job.StartAsync(CancellationToken.None);
        await sender.Reached.WaitAsync(TimeSpan.FromSeconds(10));
        await job.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Job_runs_at_startup_and_then_on_its_interval()
    {
        var sender = new RecordingSender { Enough = 3 };

        await RunUntilReachedAsync(new FastJob(Scopes(sender), TimeProvider.System), sender);

        Assert.All(sender.Sent, command => Assert.IsType<TestCommand>(command));
    }

    [Fact]
    public async Task A_failed_run_does_not_stop_the_job()
    {
        var sender = new RecordingSender { FailFirst = 2, Enough = 3 };

        await RunUntilReachedAsync(new FastJob(Scopes(sender), TimeProvider.System), sender);

        Assert.True(sender.Sent.Count >= 3);
    }

    [Fact]
    public async Task Stopping_the_host_ends_the_job_cleanly()
    {
        var sender = new RecordingSender();
        var job = new FastJob(Scopes(sender), TimeProvider.System);

        await RunUntilReachedAsync(job, sender);

        Assert.True(job.ExecuteTask!.IsCompleted);
        Assert.False(job.ExecuteTask.IsFaulted);
    }

    [Fact]
    public async Task Gps_cleanup_purges_old_location_logs_JOB_GPS_01()
    {
        var sender = new RecordingSender();

        await RunUntilReachedAsync(new GpsLogCleanupJob(Scopes(sender), TimeProvider.System, NullLogger<GpsLogCleanupJob>.Instance), sender);

        Assert.IsType<PurgeOldLocationLogsCommand>(sender.Sent[0]);
    }
}
