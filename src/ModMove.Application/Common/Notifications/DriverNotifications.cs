using System.Globalization;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Services;

namespace ModMove.Application.Common.Notifications;

/// <summary>
/// DECISIONS 8.3, B17: the driver notification events sent through <see cref="INotificationService"/>
/// (both FCM push to the mobile app and SignalR <c>NotificationReceived</c> to open connections).
/// </summary>
public enum DriverEvent
{
    /// <summary>A new eligible order within the matching radius is waiting for acceptance (Cash on creation, PayOS on payment).</summary>
    NewMatchingOrder,

    /// <summary>The customer or Dispatcher cancelled an order assigned to this driver.</summary>
    OrderCancelled,

    /// <summary>The customer approved the pickup evidence photos (order transitions to InTransit).</summary>
    PickupApproved,

    /// <summary>The customer rejected the pickup evidence photos and asked the driver to retake them.</summary>
    PickupRejected,

    /// <summary>The customer confirmed receipt or the system auto-completed the order, settling the wallet.</summary>
    OrderCompleted,

    /// <summary>The customer reported an in-transit incident on the driver's active order.</summary>
    IncidentReported,

    /// <summary>A Dispatcher resolved or decided an incident on the driver's order.</summary>
    IncidentDecided,

    /// <summary>A Dispatcher took over an incident on the driver's order (start); the text never carries the customer's words.</summary>
    IncidentInProgress,

    /// <summary>The customer rated the completed trip.</summary>
    RatingReceived,

    /// <summary>An Administrator approved the driver's onboarding application.</summary>
    ApplicationApproved,

    /// <summary>An Administrator rejected the driver's onboarding application.</summary>
    ApplicationRejected,

    /// <summary>An Administrator approved or rejected the driver's earnings withdrawal request.</summary>
    WithdrawalDecided,
}

/// <summary>
/// Standardizes driver notification event codes, Vietnamese titles/bodies, and payload keys per Table 8.3 (DECISIONS B17, 8.3).
/// </summary>
public static class DriverNotifications
{
    public const string NewMatchingOrderType = "NEW_MATCHING_ORDER";
    public const string OrderCancelledType = "ORDER_CANCELLED";
    public const string PickupApprovedType = "PICKUP_APPROVED";
    public const string PickupRejectedType = "PICKUP_REJECTED";
    public const string OrderCompletedType = "ORDER_COMPLETED";
    public const string IncidentReportedType = "INCIDENT_REPORTED";
    public const string IncidentDecidedType = "INCIDENT_DECIDED";
    public const string IncidentInProgressType = "INCIDENT_IN_PROGRESS";
    public const string RatingReceivedType = "RATING_RECEIVED";
    public const string ApplicationApprovedType = "DRIVER_APPLICATION_APPROVED";
    public const string ApplicationRejectedType = "DRIVER_APPLICATION_REJECTED";
    public const string WithdrawalDecidedType = "WITHDRAWAL_DECIDED";

    private static readonly Dictionary<DriverEvent, (string Type, string Title, string BodyTemplate)> Templates = new()
    {
        [DriverEvent.NewMatchingOrder] = (
            NewMatchingOrderType,
            "Đơn hàng mới gần bạn",
            "Đơn {0} cách bạn {1} km đang chờ nhận."),
        [DriverEvent.OrderCancelled] = (
            OrderCancelledType,
            "Đơn đã bị hủy",
            "Đơn {0} đã bị hủy."),
        [DriverEvent.PickupApproved] = (
            PickupApprovedType,
            "Ảnh lấy hàng đã được duyệt",
            "Khách đã duyệt ảnh hàng của đơn {0}. Bạn có thể bắt đầu vận chuyển."),
        [DriverEvent.PickupRejected] = (
            PickupRejectedType,
            "Ảnh lấy hàng bị từ chối",
            "Khách từ chối ảnh hàng của đơn {0}. Hãy chụp và gửi lại."),
        [DriverEvent.OrderCompleted] = (
            OrderCompletedType,
            "Đơn đã hoàn thành",
            "Đơn {0} đã hoàn thành."),
        [DriverEvent.IncidentReported] = (
            IncidentReportedType,
            "Sự cố trên đơn đang chạy",
            "Khách đã báo sự cố cho đơn {0}."),
        [DriverEvent.IncidentDecided] = (
            IncidentDecidedType,
            "Quyết định xử lý sự cố",
            "Sự cố của đơn {0} đã có quyết định từ điều phối viên."),
        [DriverEvent.IncidentInProgress] = (
            IncidentInProgressType,
            "Điều phối viên đã tiếp nhận sự cố",
            "Sự cố trên đơn {0} đang được xử lý."),
        [DriverEvent.RatingReceived] = (
            RatingReceivedType,
            "Đánh giá mới",
            "Khách đã đánh giá {1} sao cho đơn {0}."),
        [DriverEvent.ApplicationApproved] = (
            ApplicationApprovedType,
            "Hồ sơ tài xế đã được phê duyệt",
            "Chúc mừng bạn! Hồ sơ đăng ký đối tác tài xế ModMove của bạn đã được phê duyệt thành công."),
        [DriverEvent.ApplicationRejected] = (
            ApplicationRejectedType,
            "Hồ sơ tài xế bị từ chối",
            "Hồ sơ đăng ký tài xế của bạn chưa được duyệt. Vui lòng kiểm tra lý do và gửi lại thông tin."),
        [DriverEvent.WithdrawalDecided] = (
            WithdrawalDecidedType,
            "Kết quả yêu cầu rút tiền",
            "Yêu cầu rút tiền của bạn đã có kết quả."),
    };

    public static (string Type, string Title, string BodyTemplate) GetTemplate(DriverEvent driverEvent) =>
        Templates[driverEvent];

    /// <summary>Sends <c>NEW_MATCHING_ORDER</c> to an eligible nearby driver (Table 8.3).</summary>
    public static Task NotifyNewMatchingOrderAsync(
        this INotificationService notifications,
        long driverUserId,
        ShippingOrder order,
        decimal distanceKm,
        CancellationToken cancellationToken)
    {
        var (type, title, template) = Templates[DriverEvent.NewMatchingOrder];
        var distanceText = distanceKm.ToString("0.##", CultureInfo.InvariantCulture);
        var body = string.Format(CultureInfo.InvariantCulture, template, order.OrderCode, distanceText);

        var data = new Dictionary<string, string>
        {
            ["orderId"] = order.Id.ToString(CultureInfo.InvariantCulture),
            ["orderCode"] = order.OrderCode,
            ["distanceKm"] = distanceText,
        };

        return notifications.NotifyAsync(driverUserId, new Notification(type, title, body, data), cancellationToken);
    }

    /// <summary>
    /// Broadcasts <c>NEW_MATCHING_ORDER</c> to every online, approved driver within <c>MATCHING_RADIUS_KM</c>
    /// who meets all matching rules for <paramref name="order"/> (B5, GB-08..10, GB-16, DECISIONS 6.4, 8.3).
    /// </summary>
    public static async Task NotifyNearbyDriversOfNewOrderAsync(
        this INotificationService notifications,
        ShippingOrder order,
        IDriverProfileRepository driverProfiles,
        IDriverWalletRepository wallets,
        IShippingOrderRepository orders,
        IPlatformSettings settings,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var candidates = await driverProfiles.GetOnlineApprovedWithVehicleAsync(cancellationToken);
        if (candidates.Count == 0)
            return;

        decimal radiusKm = 5m;
        decimal requiredDeposit = 200_000m;
        decimal commissionRate = 0.10m;
        try
        {
            radiusKm = await settings.GetDecimalAsync(SettingKeys.MatchingRadiusKm, cancellationToken);
            requiredDeposit = await settings.GetDecimalAsync(SettingKeys.RequiredDeposit, cancellationToken);
            if (order.PaymentMethod == PaymentMethod.Cash)
            {
                commissionRate = await settings.GetDecimalAsync(SettingKeys.CommissionRate, cancellationToken);
            }
        }
        catch
        {
            // Fall back to default seeded platform settings when running in a test fixture without Settings seeded.
        }

        var orderCommission = order.PaymentMethod == PaymentMethod.Cash
            ? order.CommissionFor(commissionRate)
            : 0m;

        foreach (var profile in candidates)
        {
            if (profile.Vehicle is not { VehicleStatus: VehicleStatus.Active } vehicle
                || vehicle.VehicleTypeId != order.VehicleTypeId)
            {
                continue;
            }

            if (!profile.HasFreshLocation(now)
                || profile.LastLatitude is not { } dLat
                || profile.LastLongitude is not { } dLon)
            {
                continue;
            }

            var distKm = GeoDistance.HaversineKm(dLat, dLon, order.PickupLatitude, order.PickupLongitude);
            if (distKm > (double)radiusKm)
                continue;

            if (await orders.GetActiveOrderIdForDriverAsync(profile.Id, cancellationToken) is not null)
                continue;

            var wallet = await wallets.GetByDriverProfileIdAsync(profile.Id, cancellationToken);
            var walletEligible = order.PaymentMethod == PaymentMethod.Cash
                ? wallet is not null && wallet.CanReceiveCod(requiredDeposit, orderCommission)
                : wallet is not null && wallet.CanReceiveOrders(requiredDeposit);
            if (!walletEligible)
                continue;

            var roundedDistance = Math.Round((decimal)distKm, 2, MidpointRounding.AwayFromZero);
            await notifications.NotifyNewMatchingOrderAsync(profile.UserId, order, roundedDistance, cancellationToken);
        }
    }

    /// <summary>Sends <c>DRIVER_APPLICATION_APPROVED</c> via <see cref="INotificationService"/> (FCM + SignalR, DECISIONS B17, 8.3).</summary>
    public static Task NotifyApplicationApprovedAsync(
        this INotificationService notifications,
        long driverUserId,
        long applicationId,
        CancellationToken cancellationToken)
    {
        var (type, title, body) = Templates[DriverEvent.ApplicationApproved];
        var data = new Dictionary<string, string>
        {
            ["applicationId"] = applicationId.ToString(CultureInfo.InvariantCulture),
        };

        return notifications.NotifyAsync(driverUserId, new Notification(type, title, body, data), cancellationToken);
    }

    /// <summary>Sends <c>DRIVER_APPLICATION_REJECTED</c> via <see cref="INotificationService"/> (FCM + SignalR, DECISIONS B17, 8.3).</summary>
    public static Task NotifyApplicationRejectedAsync(
        this INotificationService notifications,
        long driverUserId,
        long applicationId,
        CancellationToken cancellationToken)
    {
        var (type, title, body) = Templates[DriverEvent.ApplicationRejected];
        var data = new Dictionary<string, string>
        {
            ["applicationId"] = applicationId.ToString(CultureInfo.InvariantCulture),
        };

        return notifications.NotifyAsync(driverUserId, new Notification(type, title, body, data), cancellationToken);
    }

    /// <summary>Sends <c>WITHDRAWAL_DECIDED</c> when an Administrator completes a withdrawal payout (Table 8.3).</summary>
    public static Task NotifyWithdrawalApprovedAsync(
        this INotificationService notifications,
        long driverUserId,
        long walletId,
        long transactionId,
        decimal amount,
        string status,
        string referenceCode,
        CancellationToken cancellationToken)
    {
        var data = new Dictionary<string, string>
        {
            ["transactionId"] = transactionId.ToString(CultureInfo.InvariantCulture),
            ["walletId"] = walletId.ToString(CultureInfo.InvariantCulture),
            ["status"] = status,
            ["referenceCode"] = referenceCode,
        };

        return notifications.NotifyAsync(
            driverUserId,
            new Notification(
                WithdrawalDecidedType,
                "Yêu cầu rút tiền đã hoàn tất",
                $"Yêu cầu rút {amount:N0} VND đã được chuyển khoản. Mã tham chiếu: {referenceCode}.",
                data),
            cancellationToken);
    }

    /// <summary>Sends <c>WITHDRAWAL_DECIDED</c> when an Administrator rejects or fails a withdrawal request (Table 8.3).</summary>
    public static Task NotifyWithdrawalRejectedAsync(
        this INotificationService notifications,
        long driverUserId,
        long walletId,
        long transactionId,
        decimal amount,
        string status,
        string? reason,
        CancellationToken cancellationToken,
        bool wrongBankAccount = false)
    {
        var reasonText = string.IsNullOrWhiteSpace(reason)
            ? string.Empty
            : $" Lý do: {reason.Trim()}";

        var data = new Dictionary<string, string>
        {
            ["transactionId"] = transactionId.ToString(CultureInfo.InvariantCulture),
            ["walletId"] = walletId.ToString(CultureInfo.InvariantCulture),
            ["status"] = status,
        };
        if (wrongBankAccount)
            data["reasonCode"] = "WRONG_BANK_ACCOUNT";

        var title = wrongBankAccount ? "Sai thông tin tài khoản rút tiền" : "Yêu cầu rút tiền bị từ chối";
        var body = wrongBankAccount
            ? $"Yêu cầu rút {amount:N0} VND không thể thực hiện do sai thông tin tài khoản nhận. Số tiền đã được hoàn lại vào ví, vui lòng cập nhật lại tài khoản ngân hàng.{reasonText}"
            : $"Yêu cầu rút {amount:N0} VND đã bị từ chối và số tiền đã được hoàn lại vào ví.{reasonText}";

        return notifications.NotifyAsync(
            driverUserId,
            new Notification(WithdrawalDecidedType, title, body, data),
            cancellationToken);
    }
}

