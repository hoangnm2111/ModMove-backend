using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ModMove.WebApi.Controllers.Public;

/// <summary>
/// Per-IP fixed window for the anonymous auth endpoints. Without it, a 6-digit OTP (5-minute life) or a
/// password could be guessed by sending requests fast; the GB-02 lock alone does not cover OTP checks.
/// <see cref="UserPolicy"/> is the per-user version for signed-in endpoints that cost money or bother other people.
/// </summary>
public static class AuthRateLimit
{
    public const string Policy = "auth";
    public const string UserPolicy = "per-user";
    public const int PermitsPerMinute = 10;

    /// <summary>One bucket per user per endpoint: customers behind one IP do not share it, and one endpoint does not use up another.</summary>
    public static string UserPartitionKey(HttpContext http) =>
        $"{http.User.FindFirstValue(ClaimTypes.NameIdentifier)}:{http.GetEndpoint()?.DisplayName}";

    // Keys on the connection IP. Behind the reverse proxy (deploy/), ASPNETCORE_FORWARDEDHEADERS_ENABLED makes it the
    // client's IP; that trusts any X-Forwarded-For, so the API port must stay reachable only from the proxy.
    public static IServiceCollection AddAuthRateLimit(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(Policy, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = PermitsPerMinute, Window = TimeSpan.FromMinutes(1) }));

            options.AddPolicy(UserPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                UserPartitionKey(http),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = PermitsPerMinute, Window = TimeSpan.FromMinutes(1) }));

            // Same error shape as every other error (ProblemDetails + stable code).
            options.OnRejected = (context, ct) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests",
                    Detail = "Too many attempts. Wait a minute and try again.",
                    Instance = context.HttpContext.Request.Path,
                };
                problem.Extensions["code"] = "TOO_MANY_REQUESTS";
                return new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json", ct));
            };
        });
}
