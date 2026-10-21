using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using ModMove.Application.Common.Interfaces;
using ModMove.Domain.Enums;

namespace ModMove.Infrastructure.Identity;

// shortcut: HS256 with one shared key and no refresh token, rotate to RS256 + refresh tokens when the clients need long sessions.
public sealed class JwtTokenService(JwtOptions options, TimeProvider clock) : IJwtTokenService
{
    public string CreateAccessToken(long userId, UserRole role)
    {
        var now = clock.GetUtcNow();
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            options.Issuer,
            options.Audience,
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString(CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Role, role.ToString()),
            ],
            notBefore: now.UtcDateTime,
            expires: now.AddMinutes(options.AccessTokenMinutes).UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
