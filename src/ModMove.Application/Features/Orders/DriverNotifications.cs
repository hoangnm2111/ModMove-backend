using System.Globalization;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Orders;

/// <summary>Customer-side order steps tell the assigned driver (DECISIONS B17: whoever changes the state notifies).</summary>
internal static class DriverNotifications
{
    public static async Task NotifyDriverAsync(
        this INotificationService notifications, IDriverProfileRepository driverProfiles, ShippingOrder order,
        string type, string title, string body, CancellationToken cancellationToken, params (string Key, string Value)[] extra)
    {
        if (order.DriverProfileId is not { } driverProfileId
            || await driverProfiles.GetByIdAsync(driverProfileId, cancellationToken) is not { } driver)
            return;

        var data = new Dictionary<string, string> { ["orderId"] = order.Id.ToString(CultureInfo.InvariantCulture) };
        foreach (var (key, value) in extra)
            data[key] = value;
        await notifications.NotifyAsync(driver.UserId, new Notification(type, title, body, data), cancellationToken);
    }
}
