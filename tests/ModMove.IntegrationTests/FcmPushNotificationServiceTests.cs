using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModMove.Application;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Notifications;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Infrastructure;
using ModMove.Infrastructure.ExternalServices.Firebase;

namespace ModMove.IntegrationTests;

/// <summary>
/// Task 4.1 &amp; 4.2 (DECISIONS C3, B17, 8.3, 9.2): <see cref="FcmPushNotificationService"/>,
/// <see cref="LogOnlyPushNotificationService"/>, <see cref="FirebaseOptions"/>, and <see cref="DriverNotifications"/>.
/// </summary>
public class FcmPushNotificationServiceTests
{
    private static readonly DateTimeOffset Now = TestApp.Start;

    private sealed class FakeFcmHandler : HttpMessageHandler
    {
        public int TokenCalls { get; private set; }
        public int SendCalls { get; private set; }
        public string? LastAssertion { get; private set; }
        public string? LastBearerToken { get; private set; }
        public string? LastSendUrl { get; private set; }
        public string? LastSendBody { get; private set; }
        public HttpStatusCode SendStatus { get; set; } = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            if (url.EndsWith("/token", StringComparison.Ordinal))
            {
                TokenCalls++;
                var form = await request.Content!.ReadAsStringAsync(cancellationToken);
                foreach (var pair in form.Split('&'))
                {
                    var kv = pair.Split('=', 2);
                    if (kv.Length == 2 && kv[0] == "assertion")
                        LastAssertion = Uri.UnescapeDataString(kv[1]);
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        $$"""{"access_token":"oauth-token-{{TokenCalls}}","expires_in":3600,"token_type":"Bearer"}""",
                        Encoding.UTF8,
                        "application/json"),
                };
            }

            SendCalls++;
            LastSendUrl = url;
            LastBearerToken = request.Headers.Authorization?.Parameter;
            LastSendBody = await request.Content!.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(SendStatus)
            {
                Content = new StringContent("""{"name":"projects/modmove-test/messages/1001"}""", Encoding.UTF8, "application/json"),
            };
        }
    }

    [Fact]
    public async Task FcmPushNotificationService_signs_jwt_assertion_caches_oauth_token_and_posts_fcm_v1_message()
    {
        using var rsa = RSA.Create(2048);
        var privateKeyPem = rsa.ExportPkcs8PrivateKeyPem();

        var options = new FirebaseOptions
        {
            ProjectId = "modmove-test",
            ClientEmail = "firebase-adminsdk@modmove-test.iam.gserviceaccount.com",
            PrivateKey = privateKeyPem,
            Endpoint = "https://fcm.example.test",
            TokenEndpoint = "https://oauth2.example.test/token",
        };

        var handler = new FakeFcmHandler();
        using var http = new HttpClient(handler);
        var clock = new MutableTime(Now);
        var service = new FcmPushNotificationService(http, options, clock);

        await service.SendAsync(
            "device-token-abc",
            "Đơn hàng mới gần bạn",
            "Đơn MM20261020001 cách bạn 1.2 km đang chờ nhận.",
            new Dictionary<string, string> { ["type"] = "NEW_MATCHING_ORDER", ["orderId"] = "42" },
            CancellationToken.None);

        Assert.Equal(1, handler.TokenCalls);
        Assert.Equal(1, handler.SendCalls);
        Assert.Equal("https://fcm.example.test/v1/projects/modmove-test/messages:send", handler.LastSendUrl);
        Assert.Equal("oauth-token-1", handler.LastBearerToken);

        // Verify the RS256 signature on the OAuth2 JWT assertion using the RSA public key
        Assert.NotNull(handler.LastAssertion);
        var parts = handler.LastAssertion!.Split('.');
        Assert.Equal(3, parts.Length);
        var signedBytes = Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}");
        var sigBytes = Convert.FromBase64String(parts[2].Replace('-', '+').Replace('_', '/') + new string('=', (4 - parts[2].Length % 4) % 4));
        Assert.True(rsa.VerifyData(signedBytes, sigBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

        // Verify FCM v1 JSON structure
        using (var doc = JsonDocument.Parse(handler.LastSendBody!))
        {
            var msg = doc.RootElement.GetProperty("message");
            Assert.Equal("device-token-abc", msg.GetProperty("token").GetString());
            Assert.Equal("Đơn hàng mới gần bạn", msg.GetProperty("notification").GetProperty("title").GetString());
            Assert.Equal("NEW_MATCHING_ORDER", msg.GetProperty("data").GetProperty("type").GetString());
            Assert.Equal("42", msg.GetProperty("data").GetProperty("orderId").GetString());
        }

        // Second call within 55 minutes reuses cached OAuth2 token
        clock.Now = Now.AddMinutes(30);
        await service.SendAsync("device-token-abc", "Title 2", "Body 2", null, CancellationToken.None);
        Assert.Equal(1, handler.TokenCalls);
        Assert.Equal(2, handler.SendCalls);

        // Advancing into the 5-minute refresh window triggers a fresh OAuth2 token exchange
        clock.Now = Now.AddMinutes(56);
        await service.SendAsync("device-token-abc", "Title 3", "Body 3", null, CancellationToken.None);
        Assert.Equal(2, handler.TokenCalls);
        Assert.Equal("oauth-token-2", handler.LastBearerToken);
    }

    [Fact]
    public async Task FcmPushNotificationService_throws_when_fcm_returns_non_success_status()
    {
        using var rsa = RSA.Create(2048);
        var options = new FirebaseOptions
        {
            ProjectId = "modmove-test",
            ClientEmail = "firebase-adminsdk@modmove-test.iam.gserviceaccount.com",
            PrivateKey = rsa.ExportPkcs8PrivateKeyPem(),
            Endpoint = "https://fcm.example.test",
            TokenEndpoint = "https://oauth2.example.test/token",
        };

        var handler = new FakeFcmHandler { SendStatus = HttpStatusCode.BadRequest };
        using var http = new HttpClient(handler);
        var service = new FcmPushNotificationService(http, options, new MutableTime(Now));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.SendAsync("bad-token", "T", "B", null, CancellationToken.None));
    }

    [Fact]
    public void FirebaseOptions_parses_service_account_json_and_rejects_half_configured_settings()
    {
        var emptyConfig = new ConfigurationBuilder().Build();
        Assert.Null(FirebaseOptions.TryLoad(emptyConfig));

        var jsonConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Firebase:CredentialsJson"] = """
                {
                  "type": "service_account",
                  "project_id": "modmove-prod",
                  "client_email": "sa@modmove-prod.iam.gserviceaccount.com",
                  "private_key": "-----BEGIN PRIVATE KEY-----\nABC\n-----END PRIVATE KEY-----\n",
                  "token_uri": "https://oauth2.googleapis.com/token"
                }
                """,
        }).Build();

        var loaded = FirebaseOptions.TryLoad(jsonConfig);
        Assert.NotNull(loaded);
        Assert.Equal("modmove-prod", loaded!.ProjectId);
        Assert.Equal("sa@modmove-prod.iam.gserviceaccount.com", loaded.ClientEmail);
        Assert.Contains("\nABC\n", loaded.PrivateKey);

        var partialConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Firebase:ProjectId"] = "modmove-prod",
        }).Build();

        Assert.Throws<InvalidOperationException>(() => FirebaseOptions.TryLoad(partialConfig));
    }

    [Fact]
    public async Task LogOnlyPushNotificationService_completes_cleanly_and_DI_switches_on_configuration()
    {
        var logOnly = new LogOnlyPushNotificationService(NullLogger<LogOnlyPushNotificationService>.Instance);
        await logOnly.SendAsync("token-1", "Title", "Body", new Dictionary<string, string> { ["type"] = "TEST" }, CancellationToken.None);

        var unconfigured = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=none;User=x;Password=y",
            ["Jwt:SigningKey"] = new string('k', 48),
        }).Build();

        var services1 = new ServiceCollection().AddLogging().AddApplication().AddInfrastructure(unconfigured);
        using var provider1 = services1.BuildServiceProvider();
        Assert.IsType<LogOnlyPushNotificationService>(provider1.GetRequiredService<IPushNotificationService>());

        using var rsa = RSA.Create(2048);
        var configured = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=none;User=x;Password=y",
            ["Jwt:SigningKey"] = new string('k', 48),
            ["Firebase:ProjectId"] = "modmove-test",
            ["Firebase:ClientEmail"] = "sa@modmove-test.iam.gserviceaccount.com",
            ["Firebase:PrivateKey"] = rsa.ExportPkcs8PrivateKeyPem(),
        }).Build();

        var services2 = new ServiceCollection().AddLogging().AddApplication().AddInfrastructure(configured);
        using var provider2 = services2.BuildServiceProvider();
        Assert.IsType<FcmPushNotificationService>(provider2.GetRequiredService<IPushNotificationService>());
    }

    [Fact]
    public async Task DriverNotifications_helpers_send_both_signalr_and_fcm_for_table_8_3_events()
    {
        var driver = User.Create("Tài", "0900000003", "tai@x.vn", "h", UserRole.Driver, emailVerified: true);
        driver.SetFcmToken("driver-fcm-token");
        var app = TestApp.SignedInAs(driver);

        var order = ShippingOrder.Create(
            "MM20261020099",
            customerId: 10L,
            vehicleTypeId: 1L,
            couponId: null,
            "A",
            21.03m,
            105.85m,
            "B",
            21.04m,
            105.86m,
            "Người nhận",
            "0912345678",
            5m,
            15,
            false,
            150_000m,
            0m,
            0m,
            PaymentMethod.Cash,
            PayerRole.Sender,
            CargoDetail.Create("FURNITURE", 1, 50, 100, 50, 50, ["a.jpg"], null),
            Now);
        TestIds.Set(order, 77L);

        var notifications = new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddSingleton<ModMove.Application.Common.Interfaces.Persistence.IUserRepository>(app.Users)
            .AddSingleton<IRealtimeNotifier>(app.Realtime)
            .AddSingleton<IPushNotificationService>(app.Push)
            .BuildServiceProvider()
            .GetRequiredService<INotificationService>();

        await notifications.NotifyNewMatchingOrderAsync(driver.Id, order, 1.35m, CancellationToken.None);
        await notifications.NotifyApplicationApprovedAsync(driver.Id, applicationId: 12L, CancellationToken.None);
        await notifications.NotifyApplicationRejectedAsync(driver.Id, applicationId: 13L, CancellationToken.None);
        await notifications.NotifyWithdrawalApprovedAsync(driver.Id, walletId: 5L, transactionId: 88L, amount: 500_000m, status: "Completed", referenceCode: "FT123", CancellationToken.None);
        await notifications.NotifyWithdrawalRejectedAsync(driver.Id, walletId: 5L, transactionId: 89L, amount: 300_000m, status: "Rejected", reason: "Sai số tài khoản", CancellationToken.None);

        Assert.Equal(5, app.Push.Sent.Count);
        Assert.Equal(5, app.Realtime.Sent.Count);

        Assert.Equal("NEW_MATCHING_ORDER", app.Push.Sent[0].Data!["type"]);
        Assert.Equal("77", app.Push.Sent[0].Data!["orderId"]);
        Assert.Equal("1.35", app.Push.Sent[0].Data!["distanceKm"]);

        Assert.Equal("DRIVER_APPLICATION_APPROVED", app.Push.Sent[1].Data!["type"]);
        Assert.Equal("12", app.Push.Sent[1].Data!["applicationId"]);

        Assert.Equal("DRIVER_APPLICATION_REJECTED", app.Push.Sent[2].Data!["type"]);
        Assert.Equal("13", app.Push.Sent[2].Data!["applicationId"]);

        Assert.Equal("WITHDRAWAL_DECIDED", app.Push.Sent[3].Data!["type"]);
        Assert.Equal("FT123", app.Push.Sent[3].Data!["referenceCode"]);

        Assert.Equal("WITHDRAWAL_DECIDED", app.Push.Sent[4].Data!["type"]);
        Assert.Contains("Sai số tài khoản", app.Push.Sent[4].Body);
    }
}

