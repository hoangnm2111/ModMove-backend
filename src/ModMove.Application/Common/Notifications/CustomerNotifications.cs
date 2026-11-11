using System.Globalization;
using ModMove.Application.Common.Interfaces;
using ModMove.Domain.Constants;
using ModMove.Domain.Entities;

namespace ModMove.Application.Common.Notifications;

/// <summary>DECISIONS 8.3: the minimum events a customer is told about. Each is sent by whoever changes the state.</summary>
public enum CustomerEvent
{
    /// <summary>A driver accepted the order (Driver, accept job).</summary>
    OrderAccepted,

    /// <summary>The driver is at the pickup point (Driver).</summary>
    DriverArrived,

    /// <summary>Pickup photos wait for the customer's review (Driver, confirm pickup).</summary>
    PickupPhotosToReview,

    /// <summary>Delivered; the customer confirms within 24 hours or it completes on its own (Driver).</summary>
    OrderDelivered,

    /// <summary>Completed by the auto-complete job (JOB-ORDER-01).</summary>
    OrderCompleted,

    /// <summary>A Dispatcher decided an incident on the order (Dispatcher).</summary>
    IncidentResolved,

    /// <summary>A refund was approved, rejected or paid out (Administrator).</summary>
    RefundDecided,

    /// <summary>The System cancelled the order: not paid in time (JOB-PAY-01) or no driver found (JOB-MATCH-01).</summary>
    OrderCancelled,

    /// <summary>R-01: a refund needs the customer's bank account before the Administrator can pay it.</summary>
    RefundBankInfoRequired,

    /// <summary>R-01: the Administrator's transfer failed; the customer fixes the account (UC-ADM-18 5a).</summary>
    RefundPayoutFailed,
}

/// <summary>The same wording for every module: call <see cref="NotifyCustomerAsync"/> after SaveChangesAsync.</summary>
public static class CustomerNotifications
{
    private static readonly Dictionary<CustomerEvent, (string Type, string Title, string Body)> Texts = new()
    {
        [CustomerEvent.OrderAccepted] = ("ORDER_ACCEPTED", "Đơn đã có tài xế", "Tài xế đã nhận đơn {0} và đang đến điểm lấy hàng."),
        [CustomerEvent.DriverArrived] = ("DRIVER_ARRIVED", "Tài xế đã đến", "Tài xế đã đến điểm lấy hàng của đơn {0}."),
        [CustomerEvent.PickupPhotosToReview] = ("PICKUP_PHOTOS_TO_REVIEW", "Ảnh lấy hàng chờ duyệt", "Xem và duyệt ảnh hàng của đơn {0} để tài xế bắt đầu vận chuyển."),
        // The windows come from BusinessRules (7.3, 7.5), so the text follows a change of the rule.
        [CustomerEvent.OrderDelivered] = ("ORDER_DELIVERED", "Đơn đã giao",
            $"Đơn {{0}} đã được giao. Xác nhận nhận hàng hoặc khiếu nại trong {BusinessRules.ComplaintWindow.TotalHours:0} giờ."),
        [CustomerEvent.OrderCompleted] = ("ORDER_COMPLETED", "Đơn đã hoàn thành",
            $"Đơn {{0}} đã hoàn thành. Bạn có thể đánh giá tài xế trong {BusinessRules.RatingWindow.TotalHours:0} giờ."),
        [CustomerEvent.IncidentResolved] = ("INCIDENT_RESOLVED", "Sự cố đã được xử lý", "Sự cố của đơn {0} đã có kết quả."),
        [CustomerEvent.RefundDecided] = ("REFUND_DECIDED", "Kết quả hoàn tiền", "Yêu cầu hoàn tiền của đơn {0} đã có kết quả."),
        [CustomerEvent.OrderCancelled] = ("ORDER_CANCELLED", "Đơn đã bị hủy", "Đơn {0} đã bị hủy."),
        // R-01: never put the account number in these texts (security.md 1).
        [CustomerEvent.RefundBankInfoRequired] = ("REFUND_BANK_INFO_REQUIRED", "Cần tài khoản nhận hoàn tiền",
            "Đơn {0} có khoản hoàn tiền. Vui lòng cung cấp tài khoản ngân hàng để nhận tiền."),
        [CustomerEvent.RefundPayoutFailed] = ("REFUND_PAYOUT_FAILED", "Chuyển hoàn tiền thất bại",
            "Chuyển hoàn tiền cho đơn {0} không thành công. Vui lòng kiểm tra lại tài khoản nhận."),
    };

    /// <summary>B17: FCM and SignalR NotificationReceived to the customer of <paramref name="order"/>; <paramref name="detail"/> is appended.</summary>
    public static Task NotifyCustomerAsync(
        this INotificationService notifications, ShippingOrder order, CustomerEvent customerEvent,
        CancellationToken cancellationToken, string? detail = null)
    {
        var (type, title, body) = Texts[customerEvent];
        var text = string.Format(CultureInfo.InvariantCulture, body, order.OrderCode);
        if (!string.IsNullOrWhiteSpace(detail))
            text += " " + detail.Trim();

        return notifications.NotifyAsync(order.CustomerId, new Notification(type, title, text,
            new Dictionary<string, string> { ["orderId"] = order.Id.ToString(CultureInfo.InvariantCulture) }), cancellationToken);
    }
}
