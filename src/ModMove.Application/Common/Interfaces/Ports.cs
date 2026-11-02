namespace ModMove.Application.Common.Interfaces;

// Ports to the outside world (SDS 2.1). Implementations live in Infrastructure/ExternalServices/<Provider>.
// Signatures are the minimum the use cases need today; the owner of each adapter may extend them.

public sealed record GeoPoint(decimal Latitude, decimal Longitude);

public sealed record RouteInfo(decimal DistanceKm, int DurationMinutes);

public sealed record PaymentLink(string CheckoutUrl, string? QrCode);

/// <summary>Cloudinary. Returns the public URL stored in the database (GB-06: JPG/PNG, at most 5 MB).</summary>
public interface IFileStorage
{
    Task<string> UploadImageAsync(Stream content, string fileName, string folder, CancellationToken cancellationToken);

    /// <summary>True only for an https URL of the team's own Cloudinary cloud (cargo, evidence and incident photos).</summary>
    bool IsOwnedUrl(string url);
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

/// <summary>PayOS: payment link/QR and webhook signature check (HMAC-SHA256).</summary>
public interface IPaymentGateway
{
    Task<PaymentLink> CreatePaymentLinkAsync(
        long payosOrderCode, decimal amount, string description, CancellationToken cancellationToken);

    bool VerifyWebhookSignature(string rawBody, string signature);
}

/// <summary>Google Maps: geocoding and route distance, Distance Matrix or Routes API (DECISIONS 3.4); distance feeds the fare.</summary>
public interface IMapService
{
    Task<GeoPoint> GeocodeAsync(string address, CancellationToken cancellationToken);

    Task<RouteInfo> GetRouteAsync(GeoPoint from, GeoPoint to, CancellationToken cancellationToken);
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
