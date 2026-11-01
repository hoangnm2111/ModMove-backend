using System.Globalization;
using ModMove.Application.Common.Interfaces;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Refunds;

/// <summary>What a Dispatcher's question on a refund tells the customer (UC-DIS-14, MSG60, B17).</summary>
internal static class DispatcherRefundNotifications
{
    /// <summary>The customer of <paramref name="order"/> is asked <see cref="Refund.InfoRequestNote"/>; call after SaveChangesAsync.</summary>
    public static Task NotifyInfoRequestedAsync(
        this INotificationService notifications, ShippingOrder order, Refund refund, CancellationToken cancellationToken) =>
        notifications.NotifyAsync(order.CustomerId, new Notification(
            "REFUND_INFO_REQUESTED",
            "Cần bổ sung thông tin",
            refund.InfoRequestNote!,
            new Dictionary<string, string>
            {
                ["orderId"] = order.Id.ToString(CultureInfo.InvariantCulture),
                ["refundId"] = refund.Id.ToString(CultureInfo.InvariantCulture),
            }), cancellationToken);
}

