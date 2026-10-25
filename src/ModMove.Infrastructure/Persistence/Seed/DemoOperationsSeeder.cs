using Microsoft.EntityFrameworkCore;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Infrastructure.Persistence.Seed;

/// <summary>
/// Development-only orders, incidents and complaints so the Dispatcher screens (Incident List, Complaint Handling
/// Console) have something to show before the customer and driver sides exist. Orders are told apart by a fixed
/// <c>MMDEMOnnn</c> code, and each incident or complaint is checked by its order, so a half-finished earlier run is
/// completed instead of duplicated.
/// </summary>
internal static class DemoOperationsSeeder
{
    private const string Photo = "https://res.cloudinary.com/demo/image/upload/sample.jpg";
    private const decimal CommissionRate = 0.10m;

    public static async Task SeedAsync(
        ApplicationDbContext db, User admin, User dispatcher, User customer, User driverUser, DriverProfile driver,
        DateTimeOffset now, CancellationToken ct)
    {
        var vehicleType = await db.Set<VehicleType>().OrderBy(v => v.MaxLoadWeightKg).FirstAsync(ct);
        var people = new People(admin, dispatcher, customer, driverUser, driver);

        // Incidents: every resolution status, both reporters, several types (DECISIONS 7.1: only 4 types).
        var i1 = await EnsureOrderAsync(db, people, vehicleType, "MMDEMO001", OrderStatus.InTransit, PaymentMethod.Cash, 180_000m, now, ct);
        await EnsureIncidentAsync(db, i1, driverUser.Id, IncidentType.VehicleBreakdown,
            "Xe bị nổ lốp trên đường, cần hỗ trợ đổi xe.", 10.8012m, 106.7130m, incident => { }, ct);

        var i2 = await EnsureOrderAsync(db, people, vehicleType, "MMDEMO002", OrderStatus.PickedUp, PaymentMethod.PayOS, 220_000m, now, ct);
        await EnsureIncidentAsync(db, i2, customer.Id, IncidentType.DamagedCargo,
            "Khách thấy hàng bị móp khi tài xế bốc lên xe.", null, null,
            incident => incident.StartHandling(dispatcher.Id), ct);

        var i3 = await EnsureOrderAsync(db, people, vehicleType, "MMDEMO003", OrderStatus.InTransit, PaymentMethod.Cash, 150_000m, now, ct);
        await EnsureIncidentAsync(db, i3, driverUser.Id, IncidentType.Accident,
            "Va chạm nhẹ, xe vẫn chạy được, hàng không hư hại.", 10.7905m, 106.6802m,
            incident =>
            {
                incident.StartHandling(dispatcher.Id);
                incident.Resolve(IncidentDecision.ContinueTrip, "Đã gọi tài xế, hàng nguyên vẹn, tiếp tục chuyến.", dispatcher.Id, now);
            }, ct);

        var i4 = await EnsureOrderAsync(db, people, vehicleType, "MMDEMO004", OrderStatus.Arrived, PaymentMethod.Cash, 130_000m, now, ct);
        await EnsureIncidentAsync(db, i4, customer.Id, IncidentType.Other,
            "Tài xế đến trễ, tôi muốn báo sự cố.", null, null,
            incident => incident.CancelIncident("Không có lý do hợp lệ, chuyến vẫn tiếp tục.", dispatcher.Id, now), ct);

        var i5 = await EnsureOrderAsync(db, people, vehicleType, "MMDEMO005", OrderStatus.Arrived, PaymentMethod.PayOS, 260_000m, now, ct);
        await EnsureIncidentAsync(db, i5, driverUser.Id, IncidentType.Other,
            "Khách không nghe máy, đã chờ quá 15 phút tại điểm lấy.", 10.7769m, 106.7009m, incident => { }, ct);

        // Complaints (Refund, SourceType Complaint), one per stage of the Complaint Handling Console. Delivered orders
        // are inside the 24-hour window (DECISIONS 7.3); the closed one belongs to an order that completed on its own.
        var c1 = await EnsureOrderAsync(db, people, vehicleType, "MMDEMO006", OrderStatus.Delivered, PaymentMethod.PayOS, 240_000m, now, ct);
        await EnsureComplaintAsync(db, c1, "Hàng bị hỏng khi giao: tủ bị trầy xước nhiều chỗ.", 240_000m,
            refund => { }, ct);

        var c2 = await EnsureOrderAsync(db, people, vehicleType, "MMDEMO007", OrderStatus.Delivered, PaymentMethod.Cash, 200_000m, now, ct);
        await EnsureComplaintAsync(db, c2, "Thiếu 2 thùng đồ so với lúc lấy hàng.", 200_000m,
            refund => refund.RequestMoreInfo("Vui lòng gửi ảnh hàng khi nhận và danh sách đồ bị thiếu."), ct);

        var c3 = await EnsureOrderAsync(db, people, vehicleType, "MMDEMO008", OrderStatus.Delivered, PaymentMethod.PayOS, 280_000m, now, ct);
        await EnsureComplaintAsync(db, c3, "Tài xế giao muộn hơn 2 giờ so với dự kiến.", 100_000m,
            refund =>
            {
                refund.RequestMoreInfo("Bạn có tin nhắn hoặc ảnh chụp thời điểm nhận hàng không?");
                refund.ReplyToInfoRequest("Tôi nhận hàng lúc 17:30 trong khi hẹn 15:00, có ảnh chụp màn hình.", [Photo]);
            }, ct);

        var c4 = await EnsureOrderAsync(db, people, vehicleType, "MMDEMO009", OrderStatus.Delivered, PaymentMethod.PayOS, 320_000m, now, ct);
        await EnsureComplaintAsync(db, c4, "Hàng bị ướt do xe không che bạt.", 320_000m,
            refund => refund.Propose(160_000m, "Ảnh giao hàng cho thấy bạt che chưa kín, đề xuất hoàn 50%.", dispatcher.Id), ct);

        var c5 = await EnsureOrderAsync(db, people, vehicleType, "MMDEMO010", OrderStatus.Completed, PaymentMethod.PayOS, 210_000m, now, ct);
        await EnsureComplaintAsync(db, c5, "Tài xế thái độ không tốt, yêu cầu hoàn tiền.", 210_000m,
            refund =>
            {
                refund.Propose(0m, "Không có bằng chứng về thái độ; ảnh giao hàng bình thường.", dispatcher.Id);
                refund.RejectRefund(admin.Id, now);
            }, ct);
        await EnsureRatingAsync(db, c5, customer.Id, driver.Id, 2, "Tài xế nói chuyện cộc lốc.", now, ct);
    }

    private static async Task<ShippingOrder> EnsureOrderAsync(
        ApplicationDbContext db, People people, VehicleType vehicleType, string code, OrderStatus target,
        PaymentMethod method, decimal fee, DateTimeOffset now, CancellationToken ct)
    {
        var existing = await db.Set<ShippingOrder>().FirstOrDefaultAsync(o => o.OrderCode == code, ct);
        if (existing is not null)
            return existing;

        var cargo = CargoDetail.Create("Đồ gia dụng", 3, 120m, 100, 60, 50, [Photo], null);
        var order = ShippingOrder.Create(
            code, people.Customer.Id, vehicleType.Id, null,
            "12 Nguyễn Huệ, Quận 1, TP.HCM", 10.7769m, 106.7009m,
            "45 Lê Văn Việt, TP. Thủ Đức, TP.HCM", 10.8473m, 106.7819m,
            "Nguyễn Văn Nhận", "0911000001", 12.4m, 35, false,
            fee, 0m, 0m, method, method == PaymentMethod.Cash ? PayerRole.Sender : null, cargo, now);

        if (method == PaymentMethod.PayOS)
            order.MarkPaid(now);

        // A Completed order is shown as finished two days ago, so the Delivered ones stay inside the complaint window.
        var at = target == OrderStatus.Completed ? now.AddDays(-2) : now;
        var driverUserId = people.DriverUser.Id;

        if (target >= OrderStatus.Accepted)
            order.Accept(people.Driver.Id, driverUserId, at);
        if (target >= OrderStatus.Arrived)
            order.ConfirmArrival(driverUserId, at);
        if (target >= OrderStatus.PickedUp)
            order.ConfirmPickup([Photo, Photo], driverUserId, at);
        if (target >= OrderStatus.InTransit)
            order.ApprovePickupEvidence(people.Customer.Id, at);
        if (target >= OrderStatus.Delivered)
            order.Deliver(Photo, driverUserId, target == OrderStatus.Completed ? now.AddDays(-2) : now.AddHours(-1));
        if (target == OrderStatus.Completed)
            order.Complete(CommissionRate, ChangedByRole.System, null, now.AddHours(-1));

        db.Add(order);
        await db.SaveChangesAsync(ct);
        return order;
    }

    private static async Task EnsureIncidentAsync(
        ApplicationDbContext db, ShippingOrder order, long reporterId, IncidentType type, string description,
        decimal? latitude, decimal? longitude, Action<InTransitIncident> advance, CancellationToken ct)
    {
        if (await db.Set<InTransitIncident>().AnyAsync(i => i.OrderId == order.Id, ct))
            return;

        var incident = InTransitIncident.Report(order.Id, reporterId, type, description, [Photo], latitude, longitude);
        advance(incident);
        db.Add(incident);
        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureComplaintAsync(
        ApplicationDbContext db, ShippingOrder order, string reason, decimal requestedAmount,
        Action<Refund> advance, CancellationToken ct)
    {
        if (await db.Set<Refund>().AnyAsync(r => r.OrderId == order.Id && r.SourceType == RefundSourceType.Complaint, ct))
            return;

        var refund = Refund.Request(order.Id, null, RefundSourceType.Complaint, reason, [Photo], requestedAmount);
        advance(refund);
        db.Add(refund);
        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureRatingAsync(
        ApplicationDbContext db, ShippingOrder order, long customerId, long driverProfileId, int stars, string comment,
        DateTimeOffset now, CancellationToken ct)
    {
        if (await db.Set<Rating>().AnyAsync(r => r.OrderId == order.Id, ct))
            return;

        db.Add(Rating.Create(order.Id, customerId, driverProfileId, stars, comment, order.CompletedAt, now));
        await db.SaveChangesAsync(ct);
    }

    private sealed record People(User Admin, User Dispatcher, User Customer, User DriverUser, DriverProfile Driver);
}
