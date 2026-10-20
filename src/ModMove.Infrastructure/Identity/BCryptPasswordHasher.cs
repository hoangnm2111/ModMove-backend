using ModMove.Application.Common.Interfaces;

namespace ModMove.Infrastructure.Identity;

/// <summary>BCrypt cost 12 (SDS data protection). The 60-character hash fits users.password_hash.</summary>
public sealed class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string passwordHash) => BCrypt.Net.BCrypt.Verify(password, passwordHash);
}
