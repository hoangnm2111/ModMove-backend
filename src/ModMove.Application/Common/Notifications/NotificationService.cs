using Microsoft.Extensions.Logging;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;

namespace ModMove.Application.Common.Notifications;

/// <summary>
/// The action is already saved when this runs, so a failed delivery, timeouts included, is logged and swallowed rather than
/// turning a done action into an error. The caller's token is not used: a customer who loses signal right after cancelling
/// must not stop the driver hearing about it. FCM is optional until its adapter is registered.
/// </summary>
internal sealed class NotificationService(
    IUserRepository users,
    IRealtimeNotifier realtime,
    ILogger<NotificationService> logger,
    IPushNotificationService? push = null) : INotificationService
{
    private static readonly TimeSpan PushTimeout = TimeSpan.FromSeconds(3); // the response must not wait on FCM

    public async Task NotifyDispatchersAsync(Notification notification, CancellationToken cancellationToken)
    {
        try
        {
            await realtime.SendToDispatchersAsync(RealtimeEvents.NotificationReceived, notification, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SignalR notification {Type} to dispatchers failed", notification.Type);
        }
    }

    public async Task PublishOrderStatusAsync(ShippingOrder order, long? driverUserId, CancellationToken cancellationToken)
    {
        var update = new OrderStatusUpdate(order.Id, order.OrderStatus.ToString());
        // Two separate sends: a failing order group must not keep the driver from hearing the change.
        try
        {
            await realtime.SendToOrderAsync(order.Id, RealtimeEvents.OrderStatusChanged, update, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "OrderStatusChanged to the followers of order {OrderId} failed", order.Id);
        }

        if (driverUserId is not { } driver)
            return;
        try
        {
            await realtime.SendToUserAsync(driver, RealtimeEvents.OrderStatusChanged, update, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "OrderStatusChanged to the driver of order {OrderId} failed", order.Id);
        }
    }

    public async Task NotifyAsync(long userId, Notification notification, CancellationToken cancellationToken)
    {
        try
        {
            await realtime.SendToUserAsync(userId, RealtimeEvents.NotificationReceived, notification, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SignalR notification {Type} to user {UserId} failed", notification.Type, userId);
        }

        if (push is null)
            return;

        try
        {
            using var timeout = new CancellationTokenSource(PushTimeout);
            var user = await users.GetByIdAsync(userId, timeout.Token);
            if (string.IsNullOrEmpty(user?.FcmToken))
                return;

            var data = new Dictionary<string, string>(notification.Data ?? new Dictionary<string, string>()) { ["type"] = notification.Type };
            await push.SendAsync(user.FcmToken, notification.Title, notification.Body, data, timeout.Token);
        }
        catch (Exception ex)
        {
            // The device token is never logged.
            logger.LogWarning(ex, "Push notification {Type} to user {UserId} failed", notification.Type, userId);
        }
    }
}
