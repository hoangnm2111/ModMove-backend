using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ModMove.Application.Common.Behaviors;

/// <summary>
/// Logs each use case with its duration; warns when one is slow.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const int SlowRequestThresholdMs = 500;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation("Handling {RequestName}", name);
        var response = await next();
        stopwatch.Stop();

        if (stopwatch.ElapsedMilliseconds > SlowRequestThresholdMs)
            logger.LogWarning("Slow request {RequestName} took {ElapsedMs} ms", name, stopwatch.ElapsedMilliseconds);
        else
            logger.LogInformation("Handled {RequestName} in {ElapsedMs} ms", name, stopwatch.ElapsedMilliseconds);

        return response;
    }
}
