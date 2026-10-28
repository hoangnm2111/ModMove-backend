using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Infrastructure.ExternalServices.Email;
using ModMove.Infrastructure.ExternalServices.GoogleMaps;
using ModMove.Infrastructure.Identity;
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

        var jwt = new JwtOptions
        {
            Issuer = configuration["Jwt:Issuer"] ?? "ModMove",
            Audience = configuration["Jwt:Audience"] ?? "ModMove.Clients",
            SigningKey = configuration["Jwt:SigningKey"] ?? "",
            AccessTokenMinutes = int.TryParse(configuration["Jwt:AccessTokenMinutes"], out var minutes) ? minutes : 15,
        };
        if (jwt.SigningKey.Length < JwtOptions.MinSigningKeyLength)
            throw new InvalidOperationException($"Jwt:SigningKey must be at least {JwtOptions.MinSigningKeyLength} characters.");
        services.AddSingleton(jwt);
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

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
        // Aggregate-specific repositories: register each next to its interface (the open generic above cannot Include).
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICouponRepository, CouponRepository>();
        services.AddScoped<IOtpRepository, OtpRepository>();
        services.AddScoped<IShippingOrderRepository, ShippingOrderRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPlatformSettingRepository, PlatformSettingRepository>();
        services.AddScoped<IDriverProfileRepository, DriverProfileRepository>();
        services.AddScoped<IDriverWalletRepository, DriverWalletRepository>();

        // Read side (Queries): raw connection for Dapper.
        services.AddSingleton<ISqlConnectionFactory>(new SqlConnectionFactory(connectionString));
        services.AddSingleton<IPlatformSettings, PlatformSettings>();

        // External adapters; ports are in Application/Common/Interfaces/Ports.cs.
        // Email is registered only when SMTP is configured; Program.cs adds a log-only sender in Development.
        if (!string.IsNullOrWhiteSpace(configuration["Email:Host"]))
        {
            services.AddSingleton(new EmailOptions
            {
                Host = configuration["Email:Host"]!,
                Port = int.TryParse(configuration["Email:Port"], out var port) ? port : 587,
                UserName = configuration["Email:UserName"] ?? "",
                Password = configuration["Email:Password"] ?? "",
                FromAddress = configuration["Email:FromAddress"] ?? configuration["Email:UserName"] ?? "",
                FromName = configuration["Email:FromName"] ?? "ModMove",
            });
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        }

        // Google Maps (DECISIONS 3.4): needed by fare estimate and order creation; registered only when a key is set.
        if (!string.IsNullOrWhiteSpace(configuration["GoogleMaps:ApiKey"]))
        {
            services.AddSingleton(new GoogleMapsOptions { ApiKey = configuration["GoogleMaps:ApiKey"]! });
            services.AddHttpClient<IMapService, GoogleMapService>();
        }

        return services;
    }
}
