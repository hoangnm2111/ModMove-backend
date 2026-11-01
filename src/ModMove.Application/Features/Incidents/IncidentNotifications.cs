using System.Globalization;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Notifications;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Incidents;

/// <summary>
/// DECISIONS 8.3, B12, B17 (TEAM-PLAN 102): after an incident decision is saved, inform the customer and the affected
/// driver(s), and publish <c>OrderStatusChanged</c> whenever the order changed state or driver.
/// </summary>
internal static class IncidentNotifications
{
    public static async Task NotifyContinueTripAsync(
        INotificationService notifications, ShippingOrder order, long? driverUserId,
        InTransitIncident incident, string reason, CancellationToken cancellationToken)
    {
        await notifications.NotifyCustomerAsync(
            order, CustomerEvent.IncidentResolved, cancellationToken, $"Chuyến đi tiếp tục. {reason}");

        await NotifyDriverDecisionAsync(
            notifications, driverUserId, order, incident, "ContinueTrip",
            "Quyết định xử lý sự cố",
            $"Điều phối viên yêu cầu tiếp tục đơn {order.OrderCode}. {reason}",
            cancellationToken);
    }

    public static async Task NotifyReassignedAsync(
        INotificationService notifications, ShippingOrder order, long? oldDriverUserId, long newDriverUserId,
        InTransitIncident incident, string reason, CancellationToken cancellationToken)
    {
        // Drivers do not join order-{id} groups (B12), so both the old and the new driver need OrderStatusChanged.
        await notifications.PublishOrderStatusAsync(order, newDriverUserId, cancellationToken);
        if (oldDriverUserId is { } oldUid && oldUid != newDriverUserId)
            await notifications.PublishOrderStatusAsync(order, oldUid, cancellationToken);

        await notifications.NotifyCustomerAsync(
            order, CustomerEvent.IncidentResolved, cancellationToken, $"Đơn đã được chuyển sang tài xế mới. {reason}");

        await NotifyDriverDecisionAsync(
            notifications, oldDriverUserId, order, incident, "ReassignDriver",
            "Đơn đã chuyển tài xế",
            $"Đơn {order.OrderCode} đã được điều phối sang tài xế khác. {reason}",
            cancellationToken);

        await NotifyDriverDecisionAsync(
            notifications, newDriverUserId, order, incident, "ReassignDriver",
            "Bạn được điều phối đơn mới",
            $"Bạn đã được phân công tiếp nhận đơn {order.OrderCode}. {reason}",
            cancellationToken);
    }

    public static async Task NotifyOrderCancelledAsync(
        INotificationService notifications, ShippingOrder order, long? driverUserId,
        InTransitIncident incident, string detailForCustomer, string detailForDriver, CancellationToken cancellationToken)
    {
        await notifications.PublishOrderStatusAsync(order, driverUserId, cancellationToken);
        await notifications.NotifyCustomerAsync(order, CustomerEvent.IncidentResolved, cancellationToken, detailForCustomer);
        await NotifyDriverDecisionAsync(
            notifications, driverUserId, order, incident, "CancelOrder",
            "Đơn đã bị hủy do sự cố",
            $"Đơn {order.OrderCode} đã bị hủy bởi điều phối viên. {detailForDriver}",
            cancellationToken);
    }

    public static async Task NotifyIncidentCancelledAsync(
        INotificationService notifications, ShippingOrder order, long? driverUserId,
        InTransitIncident incident, string reason, CancellationToken cancellationToken)
    {
        await notifications.NotifyCustomerAsync(
            order, CustomerEvent.IncidentResolved, cancellationToken, $"Báo cáo sự cố đã bị hủy. {reason}");

        await NotifyDriverDecisionAsync(
            notifications, driverUserId, order, incident, "Cancelled",
            "Báo cáo sự cố đã bị hủy",
            $"Báo cáo sự cố trên đơn {order.OrderCode} đã bị hủy. {reason}",
            cancellationToken);
    }

    private static Task NotifyDriverDecisionAsync(
        INotificationService notifications, long? driverUserId, ShippingOrder order,
        InTransitIncident incident, string decision, string title, string body, CancellationToken cancellationToken)
    {
        if (driverUserId is not { } userId)
            return Task.CompletedTask;

        var data = new Dictionary<string, string>
        {
            ["orderId"] = order.Id.ToString(CultureInfo.InvariantCulture),
            ["incidentId"] = incident.Id.ToString(CultureInfo.InvariantCulture),
            ["decision"] = decision,
        };
        return notifications.NotifyAsync(userId, new Notification("INCIDENT_DECIDED", title, body, data), cancellationToken);
    }
}

