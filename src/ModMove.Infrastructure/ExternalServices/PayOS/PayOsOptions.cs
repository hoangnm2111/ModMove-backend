using ModMove.Application.Common.Interfaces;

namespace ModMove.Infrastructure.ExternalServices.PayOS;

/// <summary>
/// Keys of the team's PayOS account (DECISIONS C3) and the URLs PayOS sends the customer back to. Read from
/// PayOS:* in appsettings.Development.json or user-secrets; never committed.
/// </summary>
public sealed class PayOsOptions
{
    public string ClientId { get; init; } = "";
    public string ApiKey { get; init; } = "";

    /// <summary>Signs the payment request and the webhook; keep it as secret as the API key.</summary>
    public string ChecksumKey { get; init; } = "";

    public string Endpoint { get; init; } = "https://api-merchant.payos.vn";

    /// <summary>
    /// The public HTTPS webhook URL registered with PayOS (e.g. via ngrok/devtunnel for local development).
    /// </summary>
    public string? WebhookUrl { get; init; }

    public string ReturnUrlWeb { get; init; } = "";
    public string ReturnUrlApp { get; init; } = "";
    public string CancelUrlWeb { get; init; } = "";
    public string CancelUrlApp { get; init; } = "";

    public string ReturnUrl(PaymentPlatform platform) =>
        Required(platform == PaymentPlatform.Web ? ReturnUrlWeb : ReturnUrlApp, $"PayOS:ReturnUrls:{platform}");

    public string CancelUrl(PaymentPlatform platform) =>
        Required(platform == PaymentPlatform.Web ? CancelUrlWeb : CancelUrlApp, $"PayOS:CancelUrls:{platform}");

    private static string Required(string value, string key) =>
        string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException($"{key} is not set.") : value;
}

