using Microsoft.EntityFrameworkCore;
using ModMove.Application.Common.Interfaces;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Infrastructure.Persistence.Seed;

/// <summary>
/// Development-only accounts, one per role, so every team member can sign in and test their
/// own role straight away. Never run in Production (see DatabaseInitializer).
/// Each part checks what already exists, so a half-finished earlier run is completed, not skipped.
/// </summary>
internal static class DemoDataSeeder
{
    public const string DefaultPassword = "ModMove@123";

    private const string PlaceholderImage = "https://res.cloudinary.com/demo/image/upload/sample.jpg";

    public static async Task SeedAsync(ApplicationDbContext db, IPasswordHasher hasher, DateTimeOffset now, CancellationToken ct)
    {
        var passwordHash = hasher.Hash(DefaultPassword);

        var admin = await EnsureUserAsync(db, "Admin ModMove", "0900000001", "admin@modmove.local", UserRole.Administrator, passwordHash, ct);
        var dispatcher = await EnsureUserAsync(db, "Dispatcher ModMove", "0900000002", "dispatcher@modmove.local", UserRole.Dispatcher, passwordHash, ct);
        var customer = await EnsureUserAsync(db, "Khách hàng Demo", "0900000003", "customer@modmove.local", UserRole.Customer, passwordHash, ct);
        var driverUser = await EnsureUserAsync(db, "Tài xế Demo", "0900000004", "driver@modmove.local", UserRole.Driver, passwordHash, ct);
        await db.SaveChangesAsync(ct);

        var profile = await db.Set<DriverProfile>()
            .Include(d => d.Vehicle)
            .FirstOrDefaultAsync(d => d.UserId == driverUser.Id, ct);

        if (profile is null)
        {
            profile = DriverProfile.Create(driverUser.Id, PlaceholderImage, PlaceholderImage);
            db.Add(profile);
            await db.SaveChangesAsync(ct);
        }

        if (profile.Vehicle is null)
        {
            var vehicleType = await db.Set<VehicleType>().OrderBy(v => v.MaxLoadWeightKg).FirstAsync(ct);

            var application = DriverApplication.Create(
                profile.Id, profile.NextVersionNumber(), vehicleType.Id,
                "51C-123.45", vehicleType.MaxLoadWeightKg, Enumerable.Repeat(PlaceholderImage, 5), now);
            profile.Submit(application);
            application.Approve(admin.Id, now);

            profile.Approve(Vehicle.Create(
                profile.Id, vehicleType.Id, "51C-123.45", vehicleType.MaxLoadWeightKg,
                $"{vehicleType.CargoBedLengthCm}x{vehicleType.CargoBedWidthCm}x{vehicleType.CargoBedHeightCm}",
                PlaceholderImage, PlaceholderImage, PlaceholderImage));
            profile.LinkBankAccount("Vietcombank", "0123456789", "TAI XE DEMO");
            await db.SaveChangesAsync(ct);
        }

        if (!await db.Set<DriverWallet>().AnyAsync(w => w.DriverProfileId == profile.Id, ct))
        {
            var wallet = DriverWallet.Create(profile.Id);
            wallet.TopUpDeposit(500_000); // above the 200,000 VND COD minimum (GB-16)
            db.Add(wallet);
            await db.SaveChangesAsync(ct);
        }

        await DemoOperationsSeeder.SeedAsync(db, admin, dispatcher, customer, driverUser, profile, now, ct);

        var driverWallet = await db.Set<DriverWallet>()
            .Include(w => w.Transactions)
            .FirstOrDefaultAsync(w => w.DriverProfileId == profile.Id, ct);
        var completedDemoOrder = await db.Set<ShippingOrder>()
            .FirstOrDefaultAsync(o => o.OrderCode == "MMDEMO010", ct);

        if (driverWallet is not null
            && completedDemoOrder is not null
            && !driverWallet.Transactions.Any(t => t.TransactionType == WalletTransactionType.Withdrawal))
        {
            if (!driverWallet.Transactions.Any(t => t.OrderId == completedDemoOrder.Id && t.TransactionType == WalletTransactionType.TripEarning))
            {
                driverWallet.CreditTripEarning(
                    completedDemoOrder.Id,
                    completedDemoOrder.TotalFee,
                    completedDemoOrder.CommissionAmount ?? completedDemoOrder.CommissionFor(0.10m));
            }

            driverWallet.RequestWithdrawal(100_000m);
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Matches by email or phone, so an existing dev account is reused instead of colliding on the unique index (GB-01).</summary>
    private static async Task<User> EnsureUserAsync(
        ApplicationDbContext db, string fullName, string phone, string email, UserRole role, string passwordHash, CancellationToken ct)
    {
        var existing = await db.Set<User>().FirstOrDefaultAsync(u => u.Email == email || u.PhoneNumber == phone, ct);
        if (existing is not null)
            return existing;

        var user = User.Create(fullName, phone, email, passwordHash, role, emailVerified: true);
        db.Add(user);
        return user;
    }
}
