using System.Data.Common;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ModMove.Application.Common.Interfaces.Persistence;

namespace ModMove.WebApi.HealthChecks;

/// <summary>/health answers Unhealthy (503) when MySQL cannot be reached, so an uptime check notices a dead database.</summary>
public sealed class DatabaseHealthCheck(ISqlConnectionFactory connections) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = connections.CreateConnection();
            if (connection is DbConnection db)
                await db.OpenAsync(cancellationToken);
            else
                connection.Open();
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The default /health writer prints only the status word, never this exception.
            return HealthCheckResult.Unhealthy("Database is unreachable.", ex);
        }
    }
}
