using System.Globalization;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Orders;

/// <summary>Customer-side order steps tell the assigned driver (DECISIONS B17: whoever changes the state notifies).</summary>
internal static class DriverNotifications
{
    /// <summary>The user id of the order's driver, or null before a driver accepted it.</summary>
    public static async Task<long?> DriverUserIdAsync(
        this IDriverProfileRepository driverProfiles, ShippingOrder order, CancellationToken cancellationToken) =>
        order.DriverProfileId is { } driverProfileId
            ? (await driverProfiles.GetByIdAsync(driverProfileId, cancellationToken))?.UserId
            : null;

    public static Task NotifyDriverAsync(
        this INotificationService notifications, long? driverUserId, ShippingOrder order,
        string type, string title, string body, CancellationToken cancellationToken, params (string Key, string Value)[] extra)
    {
        if (driverUserId is not { } userId)
            return Task.CompletedTask;

        var data = new Dictionary<string, string> { ["orderId"] = order.Id.ToString(CultureInfo.InvariantCulture) };
        foreach (var (key, value) in extra)
            data[key] = value;
        return notifications.NotifyAsync(userId, new Notification(type, title, body, data), cancellationToken);
    }
}
