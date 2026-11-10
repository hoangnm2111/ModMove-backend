using Microsoft.EntityFrameworkCore;
using ModMove.Application.Common.Interfaces;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Infrastructure.Persistence.Seed;

/// <summary>Creates the <see cref="InitialAdministrator"/> once; does nothing when any Administrator already exists.</summary>
internal static class AdministratorSeeder
{
    public static async Task<bool> SeedAsync(ApplicationDbContext db, IPasswordHasher hasher, InitialAdministrator admin, CancellationToken ct)
    {
        if (await db.Set<User>().AnyAsync(u => u.Role == UserRole.Administrator, ct))
            return false;

        if (await db.Set<User>().AnyAsync(u => u.Email == admin.Email || u.PhoneNumber == admin.PhoneNumber, ct))
            throw new InvalidOperationException("Seed:Admin email or phone number already belongs to another account.");

        db.Add(User.Create(admin.FullName, admin.PhoneNumber, admin.Email, hasher.Hash(admin.Password), UserRole.Administrator, emailVerified: true));
        await db.SaveChangesAsync(ct);
        return true;
    }
}
