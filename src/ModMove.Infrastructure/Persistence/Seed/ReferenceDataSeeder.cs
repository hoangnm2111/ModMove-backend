using Microsoft.EntityFrameworkCore;
using ModMove.Domain.Entities;

namespace ModMove.Infrastructure.Persistence.Seed;

/// <summary>
/// Reference data every environment needs: vehicle types and fares, platform settings, master data
/// and coupons (SDS 3.5, 3.7, 3.8, 3.9). Idempotent: rows are matched by their unique key and
/// never overwritten, so Administrator edits survive a restart.
/// </summary>
internal static class ReferenceDataSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        await SeedVehicleTypesAsync(db, ct);
        await SeedPlatformSettingsAsync(db, now, ct);
        await SeedMasterDataAsync(db, ct);
        await SeedCouponsAsync(db, now, ct);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedVehicleTypesAsync(ApplicationDbContext db, CancellationToken ct)
    {
        // name, max load kg, cargo bed L x W x H (cm), base fare, price per km, minimum fare (VND)
        (string Name, decimal Load, int L, int W, int H, decimal Base, decimal PerKm, decimal Min)[] rows =
        [
            ("Xe van 500kg", 500, 170, 120, 120, 50_000, 10_000, 80_000),
            ("Xe tải 1 tấn", 1_000, 300, 160, 160, 80_000, 14_000, 150_000),
            ("Xe tải 1.5 tấn", 1_500, 320, 170, 170, 100_000, 17_000, 200_000),
            ("Xe tải 2 tấn", 2_000, 400, 180, 180, 130_000, 20_000, 260_000),
        ];

        foreach (var r in rows)
        {
            if (!await db.Set<VehicleType>().AnyAsync(v => v.Name == r.Name, ct))
                db.Add(VehicleType.Create(r.Name, r.Load, r.L, r.W, r.H, r.Base, r.PerKm, r.Min));
        }
    }

    private static async Task SeedPlatformSettingsAsync(ApplicationDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        (string Key, string Value)[] rows =
        [
            ("COMMISSION_RATE", "0.10"),         // GB-14
            ("LOADING_FEE", "100000"),           // UC-GST-12
            ("PEAK_SURCHARGE", "20000"),         // UC-ADM-12
            ("BULKY_SURCHARGE", "50000"),        // UC-ADM-12
            ("MIN_COD_DEPOSIT", "200000"),       // GB-16
            ("MATCHING_TIMEOUT_MINUTES", "10"),  // GB-24
            ("PAYOS_SESSION_MINUTES", "10"),     // GB-13
            ("AUTO_COMPLETE_HOURS", "24"),       // GB-23
            ("COMPLAINT_WINDOW_HOURS", "24"),    // GB-18
        ];

        foreach (var r in rows)
        {
            if (!await db.Set<PlatformSetting>().AnyAsync(s => s.SettingKey == r.Key, ct))
                db.Add(PlatformSetting.Create(r.Key, r.Value, now));
        }
    }

    private static async Task SeedMasterDataAsync(ApplicationDbContext db, CancellationToken ct)
    {
        (string Category, string Code, string Name)[] rows =
        [
            ("CARGO_TYPE", "FURNITURE", "Đồ nội thất"),
            ("CARGO_TYPE", "APPLIANCE", "Đồ điện gia dụng"),
            ("CARGO_TYPE", "ELECTRONICS", "Thiết bị điện tử"),
            ("CARGO_TYPE", "GOODS", "Hàng hóa kinh doanh"),
            ("CARGO_TYPE", "DOCUMENT", "Tài liệu, giấy tờ"),
            ("CARGO_TYPE", "OTHER", "Khác"),
            ("INCIDENT_CATEGORY", "VEHICLE_BREAKDOWN", "Xe hỏng"),
            ("INCIDENT_CATEGORY", "DAMAGED_CARGO", "Hàng hóa hư hỏng"),
            ("INCIDENT_CATEGORY", "ACCIDENT", "Tai nạn"),
            ("INCIDENT_CATEGORY", "OTHER", "Khác"),
            ("COMPLAINT_REASON", "DAMAGED_CARGO", "Hàng bị hư hỏng"),
            ("COMPLAINT_REASON", "LATE_DELIVERY", "Giao hàng trễ"),
            ("COMPLAINT_REASON", "WRONG_ROUTE", "Đi sai lộ trình"),
            ("COMPLAINT_REASON", "SERVICE_ATTITUDE", "Thái độ phục vụ"),
            ("COMPLAINT_REASON", "OTHER", "Khác"),
            ("POLICY", "WARRANTY", "Chính sách bảo hành"),
            ("POLICY", "REFUND", "Chính sách hoàn tiền"),
            ("POLICY", "TERMS_OF_USE", "Điều khoản sử dụng"),
            ("POLICY", "PRIVACY", "Chính sách bảo mật"),
        ];

        foreach (var r in rows)
        {
            if (!await db.Set<MasterDataItem>().AnyAsync(m => m.Category == r.Category && m.Code == r.Code, ct))
                db.Add(MasterDataItem.Create(r.Category, r.Code, r.Name));
        }
    }

    private static async Task SeedCouponsAsync(ApplicationDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        // code, discount VND, minimum order VND, usage limit (null = unlimited)
        (string Code, decimal Discount, decimal MinOrder, int? Limit)[] rows =
        [
            ("WELCOME20K", 20_000, 100_000, 1_000),
            ("FREESHIP50K", 50_000, 300_000, 500),
            ("MODMOVE100K", 100_000, 1_000_000, 100),
        ];

        foreach (var r in rows)
        {
            if (!await db.Set<Coupon>().AnyAsync(c => c.DiscountCode == r.Code, ct))
                db.Add(Coupon.Create(r.Code, r.Discount, r.MinOrder, now, now.AddYears(1), r.Limit));
        }
    }
}
