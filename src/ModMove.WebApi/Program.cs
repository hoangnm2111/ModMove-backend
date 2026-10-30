using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ModMove.Infrastructure.ExternalServices.Email;
using ModMove.Infrastructure.Identity;
using ModMove.Application;
using ModMove.Application.Common.Interfaces;
using ModMove.Infrastructure;
using ModMove.Infrastructure.Persistence.Seed;
using ModMove.WebApi.Controllers;
using ModMove.WebApi.Controllers.Public;
using ModMove.WebApi.Hubs;
using ModMove.WebApi.Middlewares;
using ModMove.WebApi.Services;

const string ClientCorsPolicy = "Clients";

var builder = WebApplication.CreateBuilder(args);

// Composition root: the only place that knows every layer.
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// No SMTP configured on this machine: in Development only, write emails (OTP codes) to the log instead.
if (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(builder.Configuration["Email:Host"]))
    builder.Services.AddSingleton<IEmailSender, LogOnlyEmailSender>();

builder.Services.AddAuthRateLimit();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<JwtOptions>((bearer, jwt) =>
    bearer.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
        ClockSkew = TimeSpan.FromSeconds(30),
    });

// Enums travel as their names ("Pending"), the same form the database stores.
builder.Services.AddControllers(options => options.Conventions.Add(new RoleConvention()))
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    // Malformed JSON is rejected by MVC before our pipeline; give it the same shape as every other 400.
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(context.ModelState) { Status = StatusCodes.Status400BadRequest, Title = "Validation failed" };
        problem.Extensions["code"] = "VALIDATION";
        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập JWT Bearer token theo định dạng: Bearer {token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddSignalR();
builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();
builder.Services.AddHealthChecks();

// Clients (WebApp, mobile) are separate deployables that talk to us only over HTTP/WebSocket.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(ClientCorsPolicy, policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (app.Environment.IsDevelopment())
{
    // Code first: apply migrations and seed reference data (+ demo accounts) on startup.
    await DatabaseInitializer.MigrateAndSeedAsync(app.Services, includeDemoData: true);
}

app.UseHttpsRedirection();
app.UseCors(ClientCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Exposed for WebApplicationFactory in integration tests.
public partial class Program;
