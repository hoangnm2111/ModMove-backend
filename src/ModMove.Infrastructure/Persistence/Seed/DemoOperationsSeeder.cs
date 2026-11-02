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

        // Keep the primary demo driver Online with a fresh GPS point so the Dispatcher live map shows an active-trip marker.
        if (driver.ApprovalStatus == ApprovalStatus.Approved && driver.OnlineStatus != OnlineStatus.Online)
            driver.GoOnline();
        driver.UpdateLocation(10.8012m, 106.7130m, now.AddMinutes(-1));

        // Extra approved drivers for UC-DIS-01 (online map) and UC-DIS-06 (replacement drivers near MMDEMO001):
        // - driver2: idle, Online, fresh GPS (~0.5 km from MMDEMO001) and 500k deposit => eligible replacement & visible on map.
        // - driver3: idle, Online, stale GPS (12 min old, DECISIONS 6.4) => hidden from map and replacement candidates.
        await EnsureExtraDriverAsync(
            db, admin, vehicleType, driverUser.PasswordHash,
            "Tài xế Thay thế", "0900000005", "driver2@modmove.local",
            "079090000002", "790000000002", "51C-222.22",
            10.8050m, 106.7150m, now.AddMinutes(-1), now, ct);

        await EnsureExtraDriverAsync(
            db, admin, vehicleType, driverUser.PasswordHash,
            "Tài xế Mất GPS", "0900000006", "driver3@modmove.local",
            "079090000003", "790000000003", "51C-333.33",
            10.8030m, 106.7140m, now.AddMinutes(-12), now, ct);

        await db.SaveChangesAsync(ct);

        // Incidents: every resolution status, both reporters, several types (DECISIONS 7.1: only 4 types).
        var i1 = await EnsureOrderAsync(db, people, vehicleType, "MMDEMO001", OrderStatus.InTransit, PaymentMethod.Cash, 180_000m, now, ct);
        await EnsureIncidentAsync(db, i1, driverUser.Id, IncidentType.VehicleBreakdown,
            "Xe bị nổ lốp trên đường, cần hỗ trợ đổi xe.", 10.8012m, 106.7130m, incident => { }, ct);

        var i2 = await EnsureOrderAsync(db, people, vehicleType, "MMDEMO002", OrderStatus.PickedUp, PaymentMethod.PayOS, 220_000m, now, ct);
        var inc2 = await EnsureIncidentAsync(db, i2, customer.Id, IncidentType.DamagedCargo,
            "Khách thấy hàng bị móp khi tài xế bốc lên xe.", null, null,
            incident => incident.StartHandling(dispatcher.Id), ct);
        await EnsureIncidentRefundAsync(db, i2, inc2, "Yêu cầu bồi thường phần vỏ thùng bị móp khi bốc xếp.", 100_000m, ct);

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

        // Cover remaining statuses (PlaceOrder, Accepted, Cancelled) so UC-DIS-01 monitoring tabs have all 8 statuses.
        await EnsureOrderAsync(db, people, vehicleType, "MMDEMO011", OrderStatus.PlaceOrder, PaymentMethod.Cash, 170_000m, now, ct);
        await EnsureOrderAsync(db, people, vehicleType, "MMDEMO012", OrderStatus.Accepted, PaymentMethod.PayOS, 195_000m, now, ct);
        await EnsureOrderAsync(db, people, vehicleType, "MMDEMO013", OrderStatus.Cancelled, PaymentMethod.Cash, 160_000m, now, ct);
        await EnsureOrderAsync(db, people, vehicleType, "MMDEMO014", OrderStatus.PlaceOrder, PaymentMethod.PayOS, 200_000m, now, ct, markPaid: false);
    }

    private static async Task EnsureExtraDriverAsync(
        ApplicationDbContext db, User admin, VehicleType vehicleType, string passwordHash,
        string fullName, string phone, string email,
        string citizenId, string licenseNo, string plate,
        decimal latitude, decimal longitude, DateTimeOffset locationAt, DateTimeOffset now, CancellationToken ct)
    {
        var user = await db.Set<User>().FirstOrDefaultAsync(u => u.Email == email || u.PhoneNumber == phone, ct);
        if (user is null)
        {
            user = User.Create(fullName, phone, email, passwordHash, UserRole.Driver, emailVerified: true);
            db.Add(user);
            await db.SaveChangesAsync(ct);
        }

        var profile = await db.Set<DriverProfile>()
            .Include(d => d.Vehicle)
            .Include(d => d.Applications)
            .FirstOrDefaultAsync(d => d.UserId == user.Id, ct);

        if (profile is null)
        {
            profile = DriverProfile.Create(user.Id, citizenId, licenseNo, Photo, Photo);
            db.Add(profile);
            await db.SaveChangesAsync(ct);
        }

        if (profile.Vehicle is null)
        {
            var application = DriverApplication.Create(
                profile.Id, profile.NextVersionNumber(), vehicleType.Id, citizenId, licenseNo,
                plate, vehicleType.MaxLoadWeightKg, Enumerable.Repeat(Photo, 5), now);
            profile.Submit(application);
            application.Approve(admin.Id, now);

            profile.Approve(Vehicle.Create(
                profile.Id, vehicleType.Id, plate, vehicleType.MaxLoadWeightKg,
                $"{vehicleType.CargoBedLengthCm}x{vehicleType.CargoBedWidthCm}x{vehicleType.CargoBedHeightCm}",
                Photo, Photo, Photo));
            profile.LinkBankAccount("Vietcombank", phone, fullName.ToUpperInvariant());
            await db.SaveChangesAsync(ct);
        }

        if (!await db.Set<DriverWallet>().AnyAsync(w => w.DriverProfileId == profile.Id, ct))
        {
            var wallet = DriverWallet.Create(profile.Id);
            wallet.TopUpDeposit(500_000m);
            db.Add(wallet);
            await db.SaveChangesAsync(ct);
        }

        if (profile.ApprovalStatus == ApprovalStatus.Approved && profile.OnlineStatus != OnlineStatus.Online)
            profile.GoOnline();
        profile.UpdateLocation(latitude, longitude, locationAt);
    }

    private static async Task<ShippingOrder> EnsureOrderAsync(
        ApplicationDbContext db, People people, VehicleType vehicleType, string code, OrderStatus target,
        PaymentMethod method, decimal fee, DateTimeOffset now, CancellationToken ct, bool markPaid = true)
    {
        var existing = await db.Set<ShippingOrder>().FirstOrDefaultAsync(o => o.OrderCode == code, ct);
        if (existing is not null)
        {
            if (!markPaid && existing.PaymentStatus == PaymentStatus.Unpaid)
            {
                existing.CreatedAt = now;
                await db.SaveChangesAsync(ct);
            }
            return existing;
        }

        var cargo = CargoDetail.Create("Đồ gia dụng", 3, 120m, 100, 60, 50, [Photo], null);
        var order = ShippingOrder.Create(
            code, people.Customer.Id, vehicleType.Id, null,
            "12 Nguyễn Huệ, Quận 1, TP.HCM", 10.7769m, 106.7009m,
            "45 Lê Văn Việt, TP. Thủ Đức, TP.HCM", 10.8473m, 106.7819m,
            "Nguyễn Văn Nhận", "0911000001", 12.4m, 35, false,
            fee, 0m, 0m, method, method == PaymentMethod.Cash ? PayerRole.Sender : null, cargo, now);

        if (method == PaymentMethod.PayOS && markPaid)
            order.MarkPaid(now);

        if (target == OrderStatus.Cancelled)
        {
            order.Cancel("Khách hủy đơn trước khi ghép tài xế.", ChangedByRole.Customer, people.Customer.Id, now);
            db.Add(order);
            await db.SaveChangesAsync(ct);
            return order;
        }

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

    private static async Task<InTransitIncident> EnsureIncidentAsync(
        ApplicationDbContext db, ShippingOrder order, long reporterId, IncidentType type, string description,
        decimal? latitude, decimal? longitude, Action<InTransitIncident> advance, CancellationToken ct)
    {
        var existing = await db.Set<InTransitIncident>().FirstOrDefaultAsync(i => i.OrderId == order.Id, ct);
        if (existing is not null)
            return existing;

        var incident = InTransitIncident.Report(order.Id, reporterId, type, description, [Photo], latitude, longitude);
        advance(incident);
        db.Add(incident);
        await db.SaveChangesAsync(ct);
        return incident;
    }

    private static async Task EnsureIncidentRefundAsync(
        ApplicationDbContext db, ShippingOrder order, InTransitIncident incident, string reason, decimal requestedAmount,
        CancellationToken ct)
    {
        if (await db.Set<Refund>().AnyAsync(r => r.IncidentId == incident.Id && r.SourceType == RefundSourceType.Incident, ct))
            return;

        var refund = Refund.Request(order.Id, incident.Id, RefundSourceType.Incident, reason, [Photo], requestedAmount);
        db.Add(refund);
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
