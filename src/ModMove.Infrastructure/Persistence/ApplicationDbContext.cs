using System.Text;
using Microsoft.EntityFrameworkCore;
using ModMove.Application.Common.Interfaces.Persistence;

namespace ModMove.Infrastructure.Persistence;

/// <summary>
/// Write-side store. Entity mappings live in Persistence/Configurations
/// (one IEntityTypeConfiguration per entity) and are picked up automatically.
/// Conventions of SDS section 3 applied to every entity: snake_case column names,
/// enums stored as VARCHAR with the C# enum name.
/// </summary>
public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IUnitOfWork
{
    private const int DefaultEnumLength = 20;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));

                var clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (clrType.IsEnum)
                {
                    property.SetProviderClrType(typeof(string));
                    property.SetMaxLength(property.GetMaxLength() ?? DefaultEnumLength);
                }
            }
        }
    }

    private static string ToSnakeCase(string name)
    {
        var sb = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                    sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
