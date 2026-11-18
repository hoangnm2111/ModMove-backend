using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModMove.Application;
using ModMove.Application.Common.Interfaces;
using ModMove.Infrastructure;
using ModMove.WebApi.Controllers.Public;

namespace ModMove.IntegrationTests;

/// <summary>How the app is wired when external keys are missing or present (final review findings).</summary>
public class InfrastructureWiringTests
{
    private static IConfiguration Config(params (string Key, string Value)[] extra) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=none;User=x;Password=y",
            ["Jwt:SigningKey"] = new string('k', 48),
        }.Concat(extra.Select(e => new KeyValuePair<string, string?>(e.Key, e.Value)))).Build();

    private sealed class CapturingLogs : ILoggerProvider
    {
        public List<string> Lines { get; } = [];
        public ILogger CreateLogger(string categoryName) => new Logger(this);
        public void Dispose() { }

        private sealed class Logger(CapturingLogs owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                owner.Lines.Add(formatter(state, exception));
        }
    }

    private sealed class GeocodeOk : HttpMessageHandler
    {
        private int _requests;
        public int Requests => _requests;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requests);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"status":"OK","results":[{"geometry":{"location":{"lat":1,"lng":2}}}]}""", Encoding.UTF8, "application/json"),
            });
        }
    }

    [Fact]
    public void Gps_cleanup_job_is_registered_JOB_GPS_01()
    {
        var services = new ServiceCollection().AddLogging().AddApplication().AddInfrastructure(Config());

        Assert.Contains(services, d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
            && d.ImplementationType == typeof(ModMove.Infrastructure.BackgroundJobs.GpsLogCleanupJob));
    }

    [Fact]
    public void Refresh_token_cleanup_job_is_registered_JOB_AUTH_01()
    {
        var services = new ServiceCollection().AddLogging().AddApplication().AddInfrastructure(Config());

        Assert.Contains(services, d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
            && d.ImplementationType == typeof(ModMove.Infrastructure.BackgroundJobs.RefreshTokenCleanupJob));
    }

    [Fact]
    public void App_starts_without_maps_or_cloudinary_keys()
    {
        var services = new ServiceCollection().AddLogging().AddApplication().AddInfrastructure(Config());
        services.AddScoped<ICurrentUserService>(_ => new FakeCurrentUser(null));
        services.AddSingleton<IEmailSender>(new CapturingEmailSender()); // Program.cs adds LogOnlyEmailSender in Development
        services.AddSingleton<IRealtimeNotifier>(new CapturingRealtimeNotifier()); // Program.cs adds the SignalR adapter

        // Development turns ValidateOnBuild on: a handler whose port has no registration stops the whole app.
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.False(provider.GetRequiredService<IFileStorage>().IsOwnedUrl("https://res.cloudinary.com/x/a.jpg"));
        Assert.NotNull(provider.GetRequiredService<IMapService>());
    }

    [Fact]
    public async Task Maps_key_never_reaches_the_logs()
    {
        var logs = new CapturingLogs();
        var services = new ServiceCollection()
            .AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(logs))
            .AddInfrastructure(Config(("MapVina:ApiKey", "secret-maps-key")));
        services.AddHttpClient(nameof(IMapService)).ConfigurePrimaryHttpMessageHandler(() => new GeocodeOk());
        using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IMapService>().GeocodeAsync("Hà Nội", CancellationToken.None);

        Assert.DoesNotContain(logs.Lines, line => line.Contains("secret-maps-key"));
    }

    [Fact]
    public async Task Maps_results_are_cached_across_requests()
    {
        var stub = new GeocodeOk();
        var services = new ServiceCollection().AddLogging().AddInfrastructure(Config(("MapVina:ApiKey", "secret-maps-key")));
        services.AddHttpClient(nameof(IMapService)).ConfigurePrimaryHttpMessageHandler(() => stub);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        // Two HTTP requests = two scopes, like a fare estimate followed by the order.
        foreach (var _ in Enumerable.Range(0, 2))
        {
            using var scope = provider.CreateScope();
            var maps = scope.ServiceProvider.GetRequiredService<IMapService>();
            Assert.IsType<ModMove.Infrastructure.ExternalServices.MapVina.CachedMapService>(maps);
            await maps.GeocodeAsync("Hà Nội", CancellationToken.None);
        }

        Assert.Equal(1, stub.Requests);
    }

    [Fact]
    public void Fare_estimate_is_rate_limited_like_other_public_endpoints() =>
        Assert.Equal(
            AuthRateLimit.Policy,
            typeof(FareEstimateController).GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);

    [Theory]
    [InlineData(typeof(ModMove.WebApi.Controllers.Customer.OrdersController), nameof(ModMove.WebApi.Controllers.Customer.OrdersController.GetTracking))]
    [InlineData(typeof(ModMove.WebApi.Controllers.Customer.OrdersController), nameof(ModMove.WebApi.Controllers.Customer.OrdersController.CreatePayment))]
    [InlineData(typeof(ModMove.WebApi.Controllers.Customer.IncidentsController), nameof(ModMove.WebApi.Controllers.Customer.IncidentsController.Report))]
    [InlineData(typeof(ModMove.WebApi.Controllers.Driver.DriverController), nameof(ModMove.WebApi.Controllers.Driver.DriverController.GetRoute))] // MapVina quota
    public void Costly_customer_endpoints_are_rate_limited_per_user(Type controller, string action) =>
        Assert.Equal(AuthRateLimit.UserPolicy, controller.GetMethod(action)!.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);

    private static string UserKey(string userId, string endpoint)
    {
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId)], "test")),
        };
        http.SetEndpoint(new Microsoft.AspNetCore.Http.Endpoint(null, null, endpoint));
        return AuthRateLimit.UserPartitionKey(http);
    }

    [Fact]
    public void Each_user_has_a_bucket_for_each_endpoint()
    {
        Assert.Equal(UserKey("7", "tracking"), UserKey("7", "tracking"));
        Assert.NotEqual(UserKey("7", "tracking"), UserKey("8", "tracking"));
        Assert.NotEqual(UserKey("7", "tracking"), UserKey("7", "incidents"));
    }
}
