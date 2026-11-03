using Microsoft.Extensions.Logging;
using ModMove.Application.Common.Interfaces;

namespace ModMove.Infrastructure.ExternalServices.Firebase;

/// <summary>
/// Stand-in when Firebase credentials are not configured on a developer machine (DECISIONS C3, B17):
/// logs the notification event and title without ever logging the device token, so all workflows run locally.
/// </summary>
public sealed class LogOnlyPushNotificationService(ILogger<LogOnlyPushNotificationService> logger) : IPushNotificationService
{
    public Task SendAsync(
        string deviceToken,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var type = data is not null && data.TryGetValue("type", out var t) ? t : "UNKNOWN";
        logger.LogInformation("[DEV FCM, not sent] Type: {Type} | Title: {Title} | Body: {Body}", type, title, body);
        return Task.CompletedTask;
    }
}

