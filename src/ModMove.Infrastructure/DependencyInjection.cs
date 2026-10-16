using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Infrastructure.Persistence;
using ModMove.Infrastructure.Persistence.Interceptors;
using ModMove.Infrastructure.Persistence.Repositories;

namespace ModMove.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<AuditableEntityInterceptor>();

        // Write side (Commands): EF Core, change tracking, transactions.
        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            // Fixed server version so startup does not need a live DB connection to auto-detect it.
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36)));
            options.AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>());
        });
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        // Read side (Queries): raw connection for Dapper.
        services.AddSingleton<ISqlConnectionFactory>(new SqlConnectionFactory(connectionString));

        // External adapters (Cloudinary, GoogleMaps, PayOS, Firebase, Email) and
        // Identity (JWT, password hashing) are registered here as they are implemented.

        return services;
    }
}
