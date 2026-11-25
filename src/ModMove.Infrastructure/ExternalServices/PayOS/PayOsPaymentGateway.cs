using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModMove.Application.Common.Interfaces;
using ModMove.Domain.Constants;

namespace ModMove.Infrastructure.ExternalServices.PayOS;

/// <summary>
/// PayOS payment requests (VietQR). Creating a link is signed with HMAC-SHA256 over the five fields PayOS names, sorted by
/// name; a webhook is verified the same way over the fields of its <c>data</c> object. Nothing here ever logs a key.
/// </summary>
public sealed class PayOsPaymentGateway(HttpClient http, PayOsOptions options, ILogger<PayOsPaymentGateway> logger) : IPaymentGateway
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    private const string SuccessCode = "00";

    public async Task<PaymentLink> CreatePaymentLinkAsync(
        long payosOrderCode, decimal amount, string description, PaymentPlatform platform,
        DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        if (amount <= 0 || amount != decimal.Truncate(amount))
            throw new InvalidOperationException($"PayOS needs a whole VND amount above zero, got {amount}.");

        if (string.IsNullOrWhiteSpace(description) || description.Length > BusinessRules.PayOsMaxDescriptionLength)
            throw new InvalidOperationException($"PayOS description must be 1..{BusinessRules.PayOsMaxDescriptionLength} characters for non-linked banks, got '{description}'.");

        var vnd = (long)amount;
        var returnUrl = options.ReturnUrl(platform);
        var cancelUrl = options.CancelUrl(platform);

        // PayOS signs exactly these five fields, in this (alphabetical) order.
        var signature = Sign($"amount={vnd}&cancelUrl={cancelUrl}&description={description}&orderCode={payosOrderCode}&returnUrl={returnUrl}");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.Endpoint}/v2/payment-requests")
        {
            Content = JsonContent.Create(new
            {
                orderCode = payosOrderCode,
                amount = vnd,
                description,
                cancelUrl,
                returnUrl,
                expiredAt = expiresAt.ToUnixTimeSeconds(), // GB-13: PayOS itself stops taking the transfer after 10 minutes
                signature,
            }),
        };
        AddCredentials(request);

        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        JsonDocument json;
        try
        {
            json = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            response.EnsureSuccessStatusCode(); // an HTML error page or an empty body
            throw new InvalidOperationException("PayOS answered with something that is not JSON.");
        }

        using (json)
        {
            var code = json.RootElement.TryGetProperty("code", out var c) ? c.GetString() : null;
            if (code != SuccessCode || !json.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            {
                var desc = json.RootElement.TryGetProperty("desc", out var d) ? d.GetString() : null;
                throw new InvalidOperationException($"PayOS refused the payment link: code {code ?? "?"}, {desc ?? "no description"}.");
            }

            var checkoutUrl = data.TryGetProperty("checkoutUrl", out var url) ? url.GetString() : null;
            if (string.IsNullOrWhiteSpace(checkoutUrl))
                throw new InvalidOperationException("PayOS answered without a checkout URL.");

            var qr = data.TryGetProperty("qrCode", out var q) && q.ValueKind == JsonValueKind.String ? q.GetString() : null;
            return new PaymentLink(checkoutUrl, qr);
        }
    }

    public async Task CancelPaymentLinkAsync(long payosOrderCode, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.Endpoint}/v2/payment-requests/{payosOrderCode}/cancel")
            {
                Content = JsonContent.Create(new { cancellationReason = "Replaced by a new payment code" }),
            };
            AddCredentials(request);

            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("PayOS did not cancel the old code {OrderCode}: HTTP {Status}", payosOrderCode, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(ex, "PayOS could not be reached to cancel the old code {OrderCode}", payosOrderCode);
        }
    }

    public async Task<PayOsPaymentInfo?> GetPaymentInfoAsync(long payosOrderCode, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{options.Endpoint}/v2/payment-requests/{payosOrderCode}");
            AddCredentials(request);

            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("PayOS could not give the status of code {OrderCode}: HTTP {Status}", payosOrderCode, (int)response.StatusCode);
                return null;
            }

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = json.RootElement;
            if (!root.TryGetProperty("code", out var c) || c.GetString() != SuccessCode
                || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                return null;

            var status = data.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.String ? st.GetString() : null;
            var paid = data.TryGetProperty("amountPaid", out var ap) && ap.TryGetDecimal(out var amountPaid) ? amountPaid : 0m;

            string? bin = null, account = null, name = null;
            if (data.TryGetProperty("transactions", out var txs) && txs.ValueKind == JsonValueKind.Array && txs.GetArrayLength() > 0)
            {
                var first = txs[0];
                bin = Text(first, "counterAccountBankId");
                account = Text(first, "counterAccountNumber");
                name = Text(first, "counterAccountName");
            }

            return new PayOsPaymentInfo(string.Equals(status, "PAID", StringComparison.OrdinalIgnoreCase), paid, bin, account, name);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(ex, "PayOS could not be reached for the status of code {OrderCode}", payosOrderCode);
            return null;
        }

        static string? Text(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    public async Task ConfirmWebhookAsync(string webhookUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
            throw new InvalidOperationException("PayOS needs a non-empty webhook URL.");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.Endpoint}/confirm-webhook")
        {
            Content = JsonContent.Create(new { webhookUrl }),
        };
        AddCredentials(request);

        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        JsonDocument json;
        try
        {
            json = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            response.EnsureSuccessStatusCode();
            throw new InvalidOperationException("PayOS answered confirm-webhook with something that is not JSON.");
        }

        using (json)
        {
            var code = json.RootElement.TryGetProperty("code", out var c) ? c.GetString() : null;
            if (code != SuccessCode)
            {
                var desc = json.RootElement.TryGetProperty("desc", out var d) ? d.GetString() : null;
                throw new InvalidOperationException($"PayOS refused the webhook URL: code {code ?? "?"}, {desc ?? "no description"}.");
            }
        }
    }

    /// <summary>
    /// PayOS signs the webhook's <c>data</c> object: its fields sorted by name, joined as name=value with &amp;, then
    /// HMAC-SHA256 with the checksum key. A body that is not JSON or has no <c>data</c> never verifies.
    /// </summary>
    public bool VerifyWebhookSignature(string rawBody, string signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
            return false;

        try
        {
            using var json = JsonDocument.Parse(rawBody);
            if (!json.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                return false;

            var payload = string.Join('&', data.EnumerateObject()
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => $"{p.Name}={ValueOf(p.Value)}"));

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(Sign(payload)), Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant()));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    // How PayOS's own SDK turns a field into text: null, "null", and "undefined" become empty, everything else as written.
    private static string ValueOf(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => "",
        JsonValueKind.String => value.GetString() is null or "null" or "undefined" ? "" : value.GetString()!,
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => value.GetRawText(),
    };

    private string Sign(string payload) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.ChecksumKey), Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

    private void AddCredentials(HttpRequestMessage request)
    {
        request.Headers.Add("x-client-id", options.ClientId);
        request.Headers.Add("x-api-key", options.ApiKey);
    }
}

