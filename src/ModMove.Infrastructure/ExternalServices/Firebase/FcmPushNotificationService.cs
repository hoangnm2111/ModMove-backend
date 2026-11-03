using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ModMove.Application.Common.Interfaces;

namespace ModMove.Infrastructure.ExternalServices.Firebase;

/// <summary>
/// Firebase Cloud Messaging HTTP v1 adapter (<c>POST /v1/projects/{projectId}/messages:send</c>) (DECISIONS B17, 8.3, 9.2).
/// Signs a Google OAuth2 service-account JWT assertion with RS256 and caches the resulting bearer token until 5 minutes
/// before expiry. Device tokens and private keys are never written to logs.
/// </summary>
public sealed class FcmPushNotificationService(
    HttpClient http,
    FirebaseOptions options,
    TimeProvider clock) : IPushNotificationService
{
    private const string MessagingScope = "https://www.googleapis.com/auth/firebase.messaging";
    private const string JwtBearerGrantType = "urn:ietf:params:oauth:grant-type:jwt-bearer";
    private static readonly TimeSpan TokenRefreshMargin = TimeSpan.FromMinutes(5);
    private static readonly string EncodedHeader = Base64UrlEncode("""{"alg":"RS256","typ":"JWT"}""");

    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _cachedAccessToken;
    private DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;

    public async Task SendAsync(
        string deviceToken,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deviceToken))
            return;

        var accessToken = await GetAccessTokenAsync(cancellationToken);

        var url = $"{options.Endpoint}/v1/projects/{options.ProjectId}/messages:send";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new
        {
            message = new
            {
                token = deviceToken,
                notification = new
                {
                    title,
                    body,
                },
                data = data is { Count: > 0 } ? data : null,
            },
        });

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"FCM HTTP v1 send failed with status {(int)response.StatusCode} ({response.StatusCode}).",
                inner: null,
                statusCode: response.StatusCode);
        }
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        if (_cachedAccessToken is not null && now + TokenRefreshMargin < _tokenExpiresAt)
            return _cachedAccessToken;

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            now = clock.GetUtcNow();
            if (_cachedAccessToken is not null && now + TokenRefreshMargin < _tokenExpiresAt)
                return _cachedAccessToken;

            var assertion = CreateSignedJwtAssertion(now);
            using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, options.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = JwtBearerGrantType,
                    ["assertion"] = assertion,
                }),
            };

            using var response = await http.SendAsync(tokenRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"FCM OAuth2 token exchange failed with status {(int)response.StatusCode} ({response.StatusCode}).",
                    inner: null,
                    statusCode: response.StatusCode);
            }

            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            using var json = JsonDocument.Parse(raw);
            var accessToken = json.RootElement.TryGetProperty("access_token", out var tokenProp)
                ? tokenProp.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(accessToken))
                throw new InvalidOperationException("FCM OAuth2 response did not contain an access_token.");

            var expiresInSeconds = json.RootElement.TryGetProperty("expires_in", out var expProp) && expProp.TryGetInt32(out var secs)
                ? secs
                : 3600;

            _cachedAccessToken = accessToken;
            _tokenExpiresAt = now.AddSeconds(Math.Max(60, expiresInSeconds));
            return _cachedAccessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private string CreateSignedJwtAssertion(DateTimeOffset now)
    {
        var iat = now.ToUnixTimeSeconds();
        var exp = now.AddHours(1).ToUnixTimeSeconds();

        var payloadJson = JsonSerializer.Serialize(new
        {
            iss = options.ClientEmail,
            scope = MessagingScope,
            aud = options.TokenEndpoint,
            iat,
            exp,
        });

        var signingInput = $"{EncodedHeader}.{Base64UrlEncode(payloadJson)}";
        using var rsa = RSA.Create();
        rsa.ImportFromPem(options.PrivateKey);
        var signatureBytes = rsa.SignData(
            Encoding.UTF8.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return $"{signingInput}.{Base64UrlEncode(signatureBytes)}";
    }

    private static string Base64UrlEncode(string text) => Base64UrlEncode(Encoding.UTF8.GetBytes(text));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

