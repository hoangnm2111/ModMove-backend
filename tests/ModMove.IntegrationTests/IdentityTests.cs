using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using ModMove.Domain.Enums;
using ModMove.Infrastructure.Identity;

namespace ModMove.IntegrationTests;

public class IdentityTests
{
    private static readonly JwtOptions Options = new() { SigningKey = new string('k', 48), AccessTokenMinutes = 15 };
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static TokenValidationParameters Parameters(DateTimeOffset validationTime, string? key = null) => new()
    {
        ValidIssuer = Options.Issuer,
        ValidAudience = Options.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key ?? Options.SigningKey)),
        ClockSkew = TimeSpan.Zero,
        LifetimeValidator = (notBefore, expires, _, _) =>
            validationTime.UtcDateTime >= notBefore && validationTime.UtcDateTime <= expires,
    };

    private static ClaimsPrincipal Validate(string token, TokenValidationParameters parameters) =>
        new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);

    [Fact]
    public void Token_carries_user_id_and_role()
    {
        var token = new JwtTokenService(Options, new FixedTime(Now)).CreateAccessToken(42, UserRole.Driver);

        var principal = Validate(token, Parameters(Now));

        Assert.Equal("42", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.True(principal.IsInRole("Driver"));
    }

    [Fact]
    public void Token_expires_after_the_configured_minutes()
    {
        var token = new JwtTokenService(Options, new FixedTime(Now)).CreateAccessToken(1, UserRole.Customer);

        Validate(token, Parameters(Now.AddMinutes(15)));
        Assert.ThrowsAny<SecurityTokenException>(() => Validate(token, Parameters(Now.AddMinutes(16))));
    }

    [Fact]
    public void Token_signed_with_another_key_is_rejected()
    {
        var token = new JwtTokenService(Options, new FixedTime(Now)).CreateAccessToken(1, UserRole.Administrator);

        Assert.ThrowsAny<SecurityTokenException>(() =>
            Validate(token, Parameters(Now, key: new string('x', 48))));
    }

    [Fact]
    public void Password_hash_verifies_only_the_right_password()
    {
        var hasher = new BCryptPasswordHasher();

        var hash = hasher.Hash("ModMove@123");

        Assert.Equal(60, hash.Length); // fits users.password_hash VARCHAR(60)
        Assert.True(hasher.Verify("ModMove@123", hash));
        Assert.False(hasher.Verify("wrong", hash));
    }
}
