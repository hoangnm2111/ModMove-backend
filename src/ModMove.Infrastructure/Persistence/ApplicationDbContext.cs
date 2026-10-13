using Microsoft.EntityFrameworkCore;
using ModMove.Application.Common.Interfaces.Persistence;

namespace ModMove.Infrastructure.Persistence;

/// <summary>
/// Write-side store. Entity mappings live in Persistence/Configurations
/// (one IEntityTypeConfiguration per entity) and are picked up automatically.
/// </summary>
public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IUnitOfWork
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
