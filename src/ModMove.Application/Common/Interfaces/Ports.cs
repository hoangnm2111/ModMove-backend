namespace ModMove.Application.Common.Interfaces;

// Ports to the outside world (SDS 2.1). Implementations live in Infrastructure/ExternalServices/<Provider>.
// Signatures are the minimum the use cases need today; the owner of each adapter may extend them.

public sealed record GeoPoint(decimal Latitude, decimal Longitude);

/// <param name="Polyline">Route shape as a Google-style encoded polyline (MapVina: 5 decimals, like Google), or null when the provider sent none.</param>
public sealed record RouteInfo(decimal DistanceKm, int DurationMinutes, string? Polyline = null);

public sealed record PaymentLink(string CheckoutUrl, string? QrCode);

/// <summary>Where the customer pays from: decides which return URL PayOS sends them back to (DECISIONS 3.1).</summary>
public enum PaymentPlatform
{
    Web,
    App,
}

/// <summary>Cloudinary. Returns the public URL stored in the database (GB-06: JPG/PNG, at most 5 MB).</summary>
public interface IFileStorage
{
    Task<string> UploadImageAsync(Stream content, string fileName, string folder, CancellationToken cancellationToken);

    /// <summary>True only for an https URL of the team's own Cloudinary cloud (cargo, evidence and incident photos).</summary>
    bool IsOwnedUrl(string url);

    /// <summary>
    /// NFR 2.3: a driver's documents (ID card, licence, vehicle papers) are stored as private ("authenticated") photos.
    /// The URL returned identifies the photo but does not open it; <see cref="CreateViewUrl"/> makes a short-lived link.
    /// </summary>
    Task<string> UploadPrivateImageAsync(Stream content, string fileName, string folder, CancellationToken cancellationToken) =>
        UploadImageAsync(content, fileName, folder, cancellationToken);

    /// <summary>A link that opens a private photo for a few minutes (<c>DocumentViewLinkLifetime</c>); any other URL comes back unchanged.</summary>
    string CreateViewUrl(string storedUrl) => storedUrl;
}

/// <summary>Gmail SMTP: OTP codes and notification emails.</summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken);
}

/// <summary>Firebase Cloud Messaging push to a device token (users.fcm_token).</summary>
public interface IPushNotificationService
{
    Task SendAsync(
        string deviceToken, string title, string body,
        IReadOnlyDictionary<string, string>? data, CancellationToken cancellationToken);
}

/// <summary>PayOS: payment link/QR, cancelling a link, and the webhook signature check (HMAC-SHA256).</summary>
public interface IPaymentGateway
{
    /// <summary>
    /// A link and QR for <paramref name="amount"/> that PayOS stops accepting at <paramref name="expiresAt"/> (GB-13).
    /// The adapter picks the return and cancel URLs from configuration by <paramref name="platform"/> (DECISIONS 3.1):
    /// the client never sends a URL.
    /// </summary>
    Task<PaymentLink> CreatePaymentLinkAsync(
        long payosOrderCode, decimal amount, string description, PaymentPlatform platform,
        DateTimeOffset expiresAt, CancellationToken cancellationToken);

    /// <summary>
    /// Best effort: asks PayOS to stop accepting an older code once a new one replaced it. A failure is only logged,
    /// because a transfer that still arrives on the old code is reconciled by the webhook.
    /// </summary>
    Task CancelPaymentLinkAsync(long payosOrderCode, CancellationToken cancellationToken);

    /// <summary>
    /// Registers the public HTTPS webhook URL with PayOS (<c>POST /confirm-webhook</c>). PayOS immediately sends a
    /// signed connectivity probe (<c>orderCode = 123</c>) to <paramref name="webhookUrl"/> before returning <c>code = "00"</c>.
    /// </summary>
    Task ConfirmWebhookAsync(string webhookUrl, CancellationToken cancellationToken = default);

    bool VerifyWebhookSignature(string rawBody, string signature);
}

/// <summary>MapVina: geocoding (Search v2) and route distance (Directions v2), DECISIONS 3.4; distance feeds the fare.</summary>
public interface IMapService
{
    Task<GeoPoint> GeocodeAsync(string address, CancellationToken cancellationToken);

    Task<RouteInfo> GetRouteAsync(GeoPoint from, GeoPoint to, CancellationToken cancellationToken);
}

/// <summary>A MapVina map file (style, TileJSON, sprite, glyphs or a vector tile) already stripped of the API key.</summary>
public sealed record MapProxyResponse(byte[] Content, string ContentType, TimeSpan MaxAge);

/// <summary>
/// Lets the web draw MapVina maps without ever seeing the MapVina key: the browser asks this server for the style and every
/// file it points to, and the key is added here (DECISIONS 3.4: the key stays on the server). Only MapVina map files are served.
/// </summary>
public interface IMapProxy
{
    /// <summary>False when <c>MapVina:ApiKey</c> is not set: the endpoints answer 503 instead of failing at random.</summary>
    bool IsConfigured { get; }

    /// <summary>The MapVina street style with every URL rewritten to <paramref name="proxyBaseUrl"/> (e.g. https://host/api/public/Map).</summary>
    Task<MapProxyResponse> GetStyleAsync(string proxyBaseUrl, CancellationToken cancellationToken);

    /// <summary><paramref name="host"/> is "maps" or "tiles". Null when the path is not a MapVina map file (404).</summary>
    Task<MapProxyResponse?> GetResourceAsync(string host, string path, string query, string proxyBaseUrl, CancellationToken cancellationToken);
}

/// <summary>Event names the hub /hubs/tracking sends to clients (DECISIONS B12, B17).</summary>
public static class RealtimeEvents
{
    public const string LocationUpdated = "LocationUpdated";
    public const string OrderStatusChanged = "OrderStatusChanged";
    public const string NotificationReceived = "NotificationReceived";
}

/// <summary>SignalR push to clients following an order (group order-{id}), to one user, or to all Dispatchers.</summary>
public interface IRealtimeNotifier
{
    Task SendToOrderAsync(long orderId, string eventName, object payload, CancellationToken cancellationToken);

    /// <summary>Every open connection of the user (Clients.User, by the NameIdentifier claim).</summary>
    Task SendToUserAsync(long userId, string eventName, object payload, CancellationToken cancellationToken);

    /// <summary>Group "dispatchers": Dispatchers join it when they connect (B12).</summary>
    Task SendToDispatchersAsync(string eventName, object payload, CancellationToken cancellationToken);
}
