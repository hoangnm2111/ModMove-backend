using Microsoft.EntityFrameworkCore;
using ModMove.Application.Common.Interfaces;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Infrastructure.Persistence.Seed;

/// <summary>
/// Development-only accounts, one per role, so every team member can sign in and test their
/// own role straight away. Never run in Production (see DatabaseInitializer).
/// </summary>
internal static class DemoDataSeeder
{
    public const string DefaultPassword = "ModMove@123";

    private const string PlaceholderImage = "https://res.cloudinary.com/demo/image/upload/sample.jpg";

    public static async Task SeedAsync(ApplicationDbContext db, IPasswordHasher hasher, DateTimeOffset now, CancellationToken ct)
    {
        var passwordHash = hasher.Hash(DefaultPassword);

        var admin = await EnsureUserAsync(db, "Admin ModMove", "0900000001", "admin@modmove.local", UserRole.Administrator, passwordHash, ct);
        await EnsureUserAsync(db, "Dispatcher ModMove", "0900000002", "dispatcher@modmove.local", UserRole.Dispatcher, passwordHash, ct);
        await EnsureUserAsync(db, "Khách hàng Demo", "0900000003", "customer@modmove.local", UserRole.Customer, passwordHash, ct);
        var driverUser = await EnsureUserAsync(db, "Tài xế Demo", "0900000004", "driver@modmove.local", UserRole.Driver, passwordHash, ct);
        await db.SaveChangesAsync(ct);

        if (await db.Set<DriverProfile>().AnyAsync(d => d.UserId == driverUser.Id, ct))
            return;

        var vehicleType = await db.Set<VehicleType>().OrderBy(v => v.MaxLoadWeightKg).FirstAsync(ct);

        var profile = DriverProfile.Create(driverUser.Id, "079090000001", "790000000001", PlaceholderImage, PlaceholderImage);
        db.Add(profile);
        await db.SaveChangesAsync(ct);

        var application = DriverApplication.Create(
            profile.Id, profile.NextVersionNumber(), vehicleType.Id, profile.CitizenIdNumber, profile.DriverLicenseNumber,
            "51C-123.45", vehicleType.MaxLoadWeightKg, Enumerable.Repeat(PlaceholderImage, 5), now);
        profile.Submit(application);
        application.Approve(admin.Id, now);

        var vehicle = Vehicle.Create(
            profile.Id, vehicleType.Id, "51C-123.45", vehicleType.MaxLoadWeightKg,
            $"{vehicleType.CargoBedLengthCm}x{vehicleType.CargoBedWidthCm}x{vehicleType.CargoBedHeightCm}",
            PlaceholderImage, PlaceholderImage, PlaceholderImage);
        profile.Approve(vehicle);
        profile.LinkBankAccount("Vietcombank", "0123456789", "TAI XE DEMO");

        var wallet = DriverWallet.Create(profile.Id);
        wallet.TopUpDeposit(500_000); // above the 200,000 VND COD minimum (GB-16)
        db.Add(wallet);

        await db.SaveChangesAsync(ct);
    }

    private static async Task<User> EnsureUserAsync(
        ApplicationDbContext db, string fullName, string phone, string email, UserRole role, string passwordHash, CancellationToken ct)
    {
        var existing = await db.Set<User>().FirstOrDefaultAsync(u => u.Email == email, ct);
        if (existing is not null)
            return existing;

        var user = User.Create(fullName, phone, email, passwordHash, role, emailVerified: true);
        db.Add(user);
        return user;
    }
}
