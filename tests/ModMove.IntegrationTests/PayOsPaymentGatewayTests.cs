using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModMove.Application;
using ModMove.Application.Common.Interfaces;
using ModMove.Infrastructure;
using ModMove.Infrastructure.ExternalServices.PayOS;

namespace ModMove.IntegrationTests;

/// <summary>The PayOS adapter (DECISIONS 3.1, GB-13): the signed request, the answer, the webhook signature, and how it is wired.</summary>
public class PayOsPaymentGatewayTests
{
    private const string ChecksumKey = "checksum-key-for-tests";

    private static readonly PayOsOptions Options = new()
    {
        ClientId = "client-1",
        ApiKey = "api-key-1",
        ChecksumKey = ChecksumKey,
        ReturnUrlWeb = "https://web.test/return",
        ReturnUrlApp = "modmove://return",
        CancelUrlWeb = "https://web.test/cancel",
        CancelUrlApp = "modmove://cancel",
    };

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add((request, request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static PayOsPaymentGateway Gateway(StubHandler handler, PayOsOptions? options = null, ILogger<PayOsPaymentGateway>? logger = null) =>
        new(new HttpClient(handler), options ?? Options, logger ?? NullLogger<PayOsPaymentGateway>.Instance);

    private static string Hmac(string payload) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(ChecksumKey), Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

    private const string OkBody = """{"code":"00","desc":"success","data":{"checkoutUrl":"https://pay.payos.vn/web/abc","qrCode":"0002010102"}}""";

    private static readonly DateTimeOffset Expires = new(2026, 10, 20, 8, 10, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_posts_a_signed_request_with_the_credentials_and_expiry()
    {
        var handler = new StubHandler(HttpStatusCode.OK, OkBody);

        var link = await Gateway(handler).CreatePaymentLinkAsync(1_760_000_000_123_456, 180_000m, "ModMove MM1", PaymentPlatform.Web, Expires, CancellationToken.None);

        Assert.Equal(new PaymentLink("https://pay.payos.vn/web/abc", "0002010102"), link);
        var (request, body) = Assert.Single(handler.Calls);
        Assert.Equal("https://api-merchant.payos.vn/v2/payment-requests", request.RequestUri!.ToString());
        Assert.Equal("client-1", request.Headers.GetValues("x-client-id").Single());
        Assert.Equal("api-key-1", request.Headers.GetValues("x-api-key").Single());

        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        Assert.Equal(1_760_000_000_123_456, root.GetProperty("orderCode").GetInt64());
        Assert.Equal(180_000, root.GetProperty("amount").GetInt64());
        Assert.Equal("ModMove MM1", root.GetProperty("description").GetString());
        Assert.Equal("https://web.test/return", root.GetProperty("returnUrl").GetString());
        Assert.Equal("https://web.test/cancel", root.GetProperty("cancelUrl").GetString());
        Assert.Equal(Expires.ToUnixTimeSeconds(), root.GetProperty("expiredAt").GetInt64());
        Assert.Equal(
            Hmac("amount=180000&cancelUrl=https://web.test/cancel&description=ModMove MM1&orderCode=1760000000123456&returnUrl=https://web.test/return"),
            root.GetProperty("signature").GetString());
    }

    [Fact]
    public async Task The_app_platform_uses_the_app_return_and_cancel_urls()
    {
        var handler = new StubHandler(HttpStatusCode.OK, OkBody);

        await Gateway(handler).CreatePaymentLinkAsync(1, 5_000m, "d", PaymentPlatform.App, Expires, CancellationToken.None);

        using var json = JsonDocument.Parse(handler.Calls[0].Body);
        Assert.Equal("modmove://return", json.RootElement.GetProperty("returnUrl").GetString());
        Assert.Equal("modmove://cancel", json.RootElement.GetProperty("cancelUrl").GetString());
    }

    [Fact]
    public async Task A_platform_without_urls_in_the_configuration_is_a_server_error()
    {
        var handler = new StubHandler(HttpStatusCode.OK, OkBody);
        var webOnly = new PayOsOptions { ClientId = "c", ApiKey = "k", ChecksumKey = ChecksumKey, ReturnUrlWeb = "https://w/r", CancelUrlWeb = "https://w/c" };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Gateway(handler, webOnly).CreatePaymentLinkAsync(1, 5_000m, "d", PaymentPlatform.App, Expires, CancellationToken.None));

        Assert.Contains("PayOS:ReturnUrls:App", error.Message);
        Assert.Empty(handler.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1000.5)]
    public async Task An_amount_PayOS_cannot_take_is_refused_before_calling_it(double amount)
    {
        var handler = new StubHandler(HttpStatusCode.OK, OkBody);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Gateway(handler).CreatePaymentLinkAsync(1, (decimal)amount, "d", PaymentPlatform.Web, Expires, CancellationToken.None));

        Assert.Empty(handler.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, """{"code":"231","desc":"Order already exists"}""")]
    [InlineData(HttpStatusCode.BadRequest, """{"code":"20","desc":"Invalid signature"}""")]
    [InlineData(HttpStatusCode.OK, """{"code":"00","desc":"success","data":{"qrCode":"x"}}""")]
    public async Task A_refusal_or_an_answer_without_a_link_is_a_server_error(HttpStatusCode status, string body)
    {
        var handler = new StubHandler(status, body);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Gateway(handler).CreatePaymentLinkAsync(1, 5_000m, "d", PaymentPlatform.Web, Expires, CancellationToken.None));
    }

    [Fact]
    public async Task An_answer_that_is_not_json_fails_and_never_echoes_the_keys()
    {
        var handler = new StubHandler(HttpStatusCode.BadGateway, "<html>bad gateway</html>");

        var error = await Assert.ThrowsAnyAsync<Exception>(() =>
            Gateway(handler).CreatePaymentLinkAsync(1, 5_000m, "d", PaymentPlatform.Web, Expires, CancellationToken.None));

        Assert.DoesNotContain("api-key-1", error.ToString());
        Assert.DoesNotContain(ChecksumKey, error.ToString());
    }

    [Fact]
    public async Task Cancel_asks_PayOS_to_cancel_the_code()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"code":"00"}""");

        await Gateway(handler).CancelPaymentLinkAsync(42, CancellationToken.None);

        var (request, _) = Assert.Single(handler.Calls);
        Assert.Equal("https://api-merchant.payos.vn/v2/payment-requests/42/cancel", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task A_failed_cancel_is_only_logged_the_webhook_reconciles_the_rest()
    {
        var logs = new List<string>();
        var handler = new StubHandler(HttpStatusCode.BadRequest, """{"code":"101"}""");

        await Gateway(handler, logger: new ListLogger(logs)).CancelPaymentLinkAsync(42, CancellationToken.None); // does not throw

        Assert.Contains(logs, l => l.Contains("42"));
        Assert.DoesNotContain(logs, l => l.Contains("api-key-1"));
    }

    private sealed class ListLogger(List<string> lines) : ILogger<PayOsPaymentGateway>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            lines.Add(formatter(state, exception));
    }

    // PayOS signs the webhook's "data" object: fields sorted by name, name=value joined by &, HMAC-SHA256.
    private const string Data = """{"orderCode":1760000000123456,"amount":180000,"description":"ModMove MM1","accountNumber":"0123","reference":"FT1","counterAccountName":null,"currency":"VND"}""";

    private static readonly string DataPayload =
        "accountNumber=0123&amount=180000&counterAccountName=&currency=VND&description=ModMove MM1&orderCode=1760000000123456&reference=FT1";

    private static string Webhook(string data = Data) => $$"""{"code":"00","desc":"success","success":true,"data":{{data}},"signature":"ignored-here"}""";

    [Fact]
    public void A_webhook_signed_with_the_checksum_key_verifies()
    {
        var gateway = Gateway(new StubHandler(HttpStatusCode.OK, ""));

        Assert.True(gateway.VerifyWebhookSignature(Webhook(), Hmac(DataPayload)));
        Assert.True(gateway.VerifyWebhookSignature(Webhook(), Hmac(DataPayload).ToUpperInvariant())); // hex case does not matter
    }

    [Fact]
    public void A_webhook_with_a_changed_amount_a_wrong_key_or_no_signature_is_refused()
    {
        var gateway = Gateway(new StubHandler(HttpStatusCode.OK, ""));
        var cheaper = Data.Replace("180000", "1000");

        Assert.False(gateway.VerifyWebhookSignature(Webhook(cheaper), Hmac(DataPayload))); // body altered after signing
        Assert.False(gateway.VerifyWebhookSignature(Webhook(), Hmac(DataPayload + "x")));
        Assert.False(gateway.VerifyWebhookSignature(Webhook(), ""));
        Assert.False(gateway.VerifyWebhookSignature(Webhook(), "   "));
        var otherKey = new PayOsOptions { ClientId = "c", ApiKey = "k", ChecksumKey = "another-key" };
        Assert.False(Gateway(new StubHandler(HttpStatusCode.OK, ""), otherKey).VerifyWebhookSignature(Webhook(), Hmac(DataPayload)));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"code":"00"}""")]
    [InlineData("""{"data":"a string"}""")]
    [InlineData("")]
    public void A_body_without_a_data_object_never_verifies(string body) =>
        Assert.False(Gateway(new StubHandler(HttpStatusCode.OK, "")).VerifyWebhookSignature(body, Hmac("anything")));

    // Wiring: DependencyInjection picks the real adapter only when the keys are there.
    private static IConfiguration Config(params (string Key, string Value)[] extra) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=none;User=x;Password=y",
            ["Jwt:SigningKey"] = new string('k', 48),
        }.Concat(extra.Select(e => new KeyValuePair<string, string?>(e.Key, e.Value)))).Build();

    private static IPaymentGateway Resolve(IConfiguration configuration)
    {
        var services = new ServiceCollection().AddLogging().AddApplication().AddInfrastructure(configuration);
        return services.BuildServiceProvider().GetRequiredService<IPaymentGateway>();
    }

    [Fact]
    public async Task Without_keys_a_stand_in_fails_to_create_and_rejects_every_webhook()
    {
        var gateway = Resolve(Config());

        Assert.IsType<NotConfiguredPaymentGateway>(gateway);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gateway.CreatePaymentLinkAsync(1, 5_000m, "d", PaymentPlatform.Web, Expires, CancellationToken.None));
        await gateway.CancelPaymentLinkAsync(1, CancellationToken.None); // nothing to cancel, no error
        Assert.False(gateway.VerifyWebhookSignature(Webhook(), Hmac(DataPayload))); // even a correctly signed one
    }

    [Fact]
    public void With_keys_the_real_adapter_is_used()
    {
        var gateway = Resolve(Config(("PayOS:ClientId", "c"), ("PayOS:ApiKey", "k"), ("PayOS:ChecksumKey", "s")));

        Assert.IsType<PayOsPaymentGateway>(gateway);
    }

    [Theory]
    [InlineData("", "s")]
    [InlineData("k", "")]
    public void A_client_id_without_the_other_keys_stops_the_app(string apiKey, string checksumKey)
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            Resolve(Config(("PayOS:ClientId", "c"), ("PayOS:ApiKey", apiKey), ("PayOS:ChecksumKey", checksumKey))));

        Assert.Contains("PayOS:ApiKey", error.Message);
    }
}

