using System.Security.Cryptography;
using System.Text;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Auth;

internal static class RefreshTokens
{
    /// <summary>32 random bytes, URL-safe, for the client to keep; only its hash goes to the database.</summary>
    public static string Issue(IRefreshTokenRepository tokens, long userId, DateTimeOffset now)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        tokens.Add(RefreshToken.Issue(userId, Hash(raw), now));
        return raw;
    }

    // SHA-256, not BCrypt: the token is already 256 random bits, and lookup needs the same hash every time.
    public static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
}
