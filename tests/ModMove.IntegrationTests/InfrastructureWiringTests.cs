using System.Net;
using System.Reflection;
using System.Text;
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
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"status":"OK","results":[{"geometry":{"location":{"lat":1,"lng":2}}}]}""", Encoding.UTF8, "application/json"),
            });
    }

    [Fact]
    public void App_starts_without_maps_or_cloudinary_keys()
    {
        var services = new ServiceCollection().AddLogging().AddApplication().AddInfrastructure(Config());
        services.AddScoped<ICurrentUserService>(_ => new FakeCurrentUser(null));
        services.AddSingleton<IEmailSender>(new CapturingEmailSender()); // Program.cs adds LogOnlyEmailSender in Development

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
            .AddInfrastructure(Config(("GoogleMaps:ApiKey", "secret-maps-key")));
        services.AddHttpClient(nameof(IMapService)).ConfigurePrimaryHttpMessageHandler(() => new GeocodeOk());
        using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IMapService>().GeocodeAsync("Hà Nội", CancellationToken.None);

        Assert.DoesNotContain(logs.Lines, line => line.Contains("secret-maps-key"));
    }

    [Fact]
    public void Fare_estimate_is_rate_limited_like_other_public_endpoints() =>
        Assert.Equal(
            AuthRateLimit.Policy,
            typeof(FareEstimateController).GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);
}
