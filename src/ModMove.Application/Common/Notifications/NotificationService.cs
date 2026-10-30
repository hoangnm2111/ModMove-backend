using Microsoft.Extensions.Logging;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;

namespace ModMove.Application.Common.Notifications;

/// <summary>
/// The action is already saved when this runs, so a failed delivery is logged and swallowed rather than turning a done
/// action into an error. FCM is optional until its adapter is registered.
/// </summary>
internal sealed class NotificationService(
    IUserRepository users,
    IRealtimeNotifier realtime,
    ILogger<NotificationService> logger,
    IPushNotificationService? push = null) : INotificationService
{
    public async Task NotifyAsync(long userId, Notification notification, CancellationToken cancellationToken)
    {
        try
        {
            await realtime.SendToUserAsync(userId, RealtimeEvents.NotificationReceived, notification, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "SignalR notification {Type} to user {UserId} failed", notification.Type, userId);
        }

        if (push is null)
            return;

        try
        {
            var user = await users.GetByIdAsync(userId, cancellationToken);
            if (string.IsNullOrEmpty(user?.FcmToken))
                return;

            var data = new Dictionary<string, string>(notification.Data ?? new Dictionary<string, string>()) { ["type"] = notification.Type };
            await push.SendAsync(user.FcmToken, notification.Title, notification.Body, data, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The device token is never logged.
            logger.LogWarning(ex, "Push notification {Type} to user {UserId} failed", notification.Type, userId);
        }
    }
}
