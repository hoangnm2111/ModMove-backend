namespace ModMove.Application.Common.Interfaces;

/// <summary>
/// One notification for one user. <see cref="Type"/> is a stable code the clients switch on (for example ORDER_ACCEPTED);
/// <see cref="Data"/> carries ids such as orderId.
/// </summary>
public sealed record Notification(string Type, string Title, string Body, IReadOnlyDictionary<string, string>? Data = null);

/// <summary>Payload of the OrderStatusChanged event (B12).</summary>
public sealed record OrderStatusUpdate(long OrderId, string OrderStatus);

/// <summary>
/// DECISIONS B17, 8.3: sends a notification both ways in one call: FCM to the user's app and SignalR NotificationReceived
/// to every open connection of the user. Nothing is stored. Call it after SaveChangesAsync; whoever changes the state sends it.
/// </summary>
public interface INotificationService
{
    Task NotifyAsync(long userId, Notification notification, CancellationToken cancellationToken);

    /// <summary>SignalR NotificationReceived to the group "dispatchers" (B12); the Dispatcher web has no FCM.</summary>
    Task NotifyDispatchersAsync(Notification notification, CancellationToken cancellationToken);

    /// <summary>
    /// B12: OrderStatusChanged with the order's current status to the customers following it (group order-{id}) and to
    /// the driver (<paramref name="driverUserId"/>, who does not join order groups). Call after every saved status change.
    /// </summary>
    Task PublishOrderStatusAsync(Domain.Entities.ShippingOrder order, long? driverUserId, CancellationToken cancellationToken);
}
