using System.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Infrastructure.Persistence.Seed;
using ModMove.WebApi.HealthChecks;
using ModMove.WebApi.Services;

namespace ModMove.IntegrationTests;

/// <summary>What Production needs before it starts: every external key, a first Administrator, a /health that sees the DB.</summary>
public class ProductionReadinessTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    private static readonly (string, string?)[] AllExternalKeys =
    [
        ("MapVina:ApiKey", "k"),
        ("Cloudinary:CloudName", "c"), ("Cloudinary:ApiKey", "k"), ("Cloudinary:ApiSecret", "s"),
        ("PayOS:ClientId", "id"), ("PayOS:ReturnUrls:Web", "https://modmove.vn/orders"), ("PayOS:CancelUrls:Web", "https://modmove.vn/orders"),
        ("Email:Host", "smtp.example.com"), ("Email:UserName", "u"), ("Email:Password", "p"),
    ];

    [Fact]
    public void Production_refuses_to_start_and_names_every_missing_key()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfiguration.EnsureComplete(Config(("MapVina:ApiKey", "k"), ("Email:Host", " "))));

        Assert.Contains("Email:Host", error.Message);
        Assert.Contains("Cloudinary:CloudName", error.Message);
        Assert.Contains("PayOS:ClientId", error.Message);
        Assert.DoesNotContain("MapVina:ApiKey", error.Message);
    }

    [Fact]
    public void Production_starts_when_every_external_service_is_configured() =>
        ProductionConfiguration.EnsureComplete(Config(AllExternalKeys));

    [Fact]
    public void Initial_administrator_is_optional() =>
        Assert.Null(InitialAdministrator.FromConfiguration(Config()));

    [Fact]
    public void Initial_administrator_password_without_the_rest_stops_the_app() =>
        Assert.Throws<InvalidOperationException>(() => InitialAdministrator.FromConfiguration(Config(("Seed:Admin:Password", "Strong-pass-123"))));

    /// <summary>deploy/README tells the operator to delete the password after the first start; the app must still start.</summary>
    [Fact]
    public void Initial_administrator_is_skipped_once_its_password_is_removed() =>
        Assert.Null(InitialAdministrator.FromConfiguration(Config(
            ("Seed:Admin:Email", "admin@modmove.vn"), ("Seed:Admin:PhoneNumber", "0900000009"), ("Seed:Admin:Password", ""))));

    [Theory]
    [InlineData("admin@modmove.vn", "0900000009", "short1")]
    [InlineData("admin@modmove.vn", "0900000009", "onlyletters")]
    [InlineData("admin@modmove.vn", "0900000009", "12345678")]
    [InlineData("admin@modmove.vn", "", "Strong-pass-123")]
    [InlineData("admin@modmove.vn", "12345", "Strong-pass-123")]
    [InlineData("", "0900000009", "Strong-pass-123")]
    [InlineData("not-an-email", "0900000009", "Strong-pass-123")]
    [InlineData("quản.trị@modmove.vn", "0900000009", "Strong-pass-123")]
    public void Initial_administrator_needs_a_valid_email_phone_and_strong_password(string email, string phone, string password) =>
        Assert.Throws<InvalidOperationException>(() => InitialAdministrator.FromConfiguration(Config(
            ("Seed:Admin:Email", email), ("Seed:Admin:PhoneNumber", phone), ("Seed:Admin:Password", password))));

    [Fact]
    public void Initial_administrator_reads_its_fields_and_never_prints_the_password()
    {
        var admin = InitialAdministrator.FromConfiguration(Config(
            ("Seed:Admin:Email", " Admin@ModMove.vn "), ("Seed:Admin:PhoneNumber", "0900000009"), ("Seed:Admin:Password", "Strong-pass-123")))!;

        Assert.Equal("admin@modmove.vn", admin.Email);
        Assert.Equal("0900000009", admin.PhoneNumber);
        Assert.Equal("Administrator", admin.FullName);
        Assert.Equal("Strong-pass-123", admin.Password);
        Assert.DoesNotContain("Strong-pass-123", admin.ToString());
    }

    private sealed class UnreachableDatabase : ISqlConnectionFactory
    {
        public IDbConnection CreateConnection() => throw new InvalidOperationException("Unable to connect to any of the specified MySQL hosts.");
    }

    [Fact]
    public async Task Health_check_is_unhealthy_when_the_database_is_unreachable()
    {
        var result = await new DatabaseHealthCheck(new UnreachableDatabase())
            .CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}
