using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModMove.Application.Common.Interfaces;

namespace ModMove.Infrastructure.Persistence.Seed;

public static class DatabaseInitializer
{
    /// <summary>
    /// Applies pending migrations, then seeds reference data (always) and demo accounts
    /// (only when <paramref name="includeDemoData"/>, i.e. Development).
    /// </summary>
    public static async Task MigrateAndSeedAsync(IServiceProvider services, bool includeDemoData, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));
        var now = sp.GetRequiredService<TimeProvider>().GetUtcNow();

        await db.Database.MigrateAsync(ct);
        await ReferenceDataSeeder.SeedAsync(db, now, ct);

        if (!includeDemoData)
            return;

        // The BCrypt adapter arrives with the Auth feature; until then demo accounts are skipped.
        var hasher = sp.GetService<IPasswordHasher>();
        if (hasher is null)
        {
            logger.LogWarning("IPasswordHasher is not registered; demo accounts were not seeded.");
            return;
        }

        await DemoDataSeeder.SeedAsync(db, hasher, now, ct);
        logger.LogInformation("Demo accounts ready (password {Password}).", DemoDataSeeder.DefaultPassword);
    }
}
