using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;
using ModMove.Infrastructure.ExternalServices.Cloudinary;
using ModMove.Infrastructure.ExternalServices.Email;
using ModMove.Infrastructure.BackgroundJobs;
using ModMove.Infrastructure.ExternalServices.GoogleMaps;
using ModMove.Infrastructure.ExternalServices.PayOS;
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
        services.AddScoped<IRefundRepository, RefundRepository>();
        services.AddScoped<IIncidentRepository, IncidentRepository>();
        services.AddScoped<IRepository<InTransitIncident>>(sp => sp.GetRequiredService<IIncidentRepository>());
        services.AddScoped<IRatingRepository, RatingRepository>();
        services.AddScoped<IMasterDataRepository, MasterDataRepository>();
        services.AddScoped<IDriverLocationLogRepository, DriverLocationLogRepository>();
        services.AddScoped<IPaymentTransactionRepository, PaymentTransactionRepository>();

        services.AddHostedService<GpsLogCleanupJob>();

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

        // Google Maps (DECISIONS 3.4) for fare estimate and order creation. Without a key the app still starts
        // (a stand-in fails only when called), so teammates without the key can run everything else.
        if (!string.IsNullOrWhiteSpace(configuration["GoogleMaps:ApiKey"]))
        {
            services.AddSingleton(new GoogleMapsOptions { ApiKey = configuration["GoogleMaps:ApiKey"]! });
            // The Geocoding URL carries the key: the default HttpClient logs print request URIs, so they are removed.
            services.AddHttpClient<IMapService, GoogleMapService>().RemoveAllLoggers();
        }
        else
        {
            services.AddSingleton<IMapService, NotConfiguredMapService>();
        }

        // Cloudinary (GB-06): image uploads and the "is this URL ours" check used by validators. Same stand-in rule.
        if (string.IsNullOrWhiteSpace(configuration["Cloudinary:CloudName"]))
        {
            services.AddSingleton<IFileStorage, NotConfiguredFileStorage>();
        }
        else
        {
            services.AddSingleton(new CloudinaryOptions
            {
                CloudName = configuration["Cloudinary:CloudName"]!,
                ApiKey = configuration["Cloudinary:ApiKey"] ?? "",
                ApiSecret = configuration["Cloudinary:ApiSecret"] ?? "",
                UploadPreset = configuration["Cloudinary:UploadPreset"] ?? "modmove",
            });
            services.AddSingleton<IFileStorage, CloudinaryFileStorage>();
        }

        // PayOS (GB-13, DECISIONS 3.1): payment links and the webhook signature. Same stand-in rule as above, but the
        // stand-in rejects every webhook. Half-filled keys stop the app, like a short Jwt:SigningKey does.
        if (string.IsNullOrWhiteSpace(configuration["PayOS:ClientId"]))
        {
            services.AddSingleton<IPaymentGateway, NotConfiguredPaymentGateway>();
        }
        else
        {
            var payOs = new PayOsOptions
            {
                ClientId = configuration["PayOS:ClientId"]!,
                ApiKey = configuration["PayOS:ApiKey"] ?? "",
                ChecksumKey = configuration["PayOS:ChecksumKey"] ?? "",
                Endpoint = configuration["PayOS:Endpoint"] is { Length: > 0 } endpoint ? endpoint.TrimEnd('/') : new PayOsOptions().Endpoint,
                ReturnUrlWeb = configuration["PayOS:ReturnUrls:Web"] ?? "",
                ReturnUrlApp = configuration["PayOS:ReturnUrls:App"] ?? "",
                CancelUrlWeb = configuration["PayOS:CancelUrls:Web"] ?? "",
                CancelUrlApp = configuration["PayOS:CancelUrls:App"] ?? "",
            };
            if (string.IsNullOrWhiteSpace(payOs.ApiKey) || string.IsNullOrWhiteSpace(payOs.ChecksumKey))
                throw new InvalidOperationException("PayOS:ClientId is set, so PayOS:ApiKey and PayOS:ChecksumKey must be set too.");
            services.AddSingleton(payOs);
            services.AddHttpClient<IPaymentGateway, PayOsPaymentGateway>();
        }

        return services;
    }
}
