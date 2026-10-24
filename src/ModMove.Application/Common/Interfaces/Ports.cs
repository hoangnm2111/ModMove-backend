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

/// <summary>SignalR push to clients following an order (group order-{id}) or one user.</summary>
public interface IRealtimeNotifier
{
    Task SendToOrderAsync(long orderId, string eventName, object payload, CancellationToken cancellationToken);

    Task SendToUserAsync(long userId, string eventName, object payload, CancellationToken cancellationToken);
}
