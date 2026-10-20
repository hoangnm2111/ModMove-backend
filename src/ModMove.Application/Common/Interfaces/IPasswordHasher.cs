namespace ModMove.Application.Common.Interfaces;

/// <summary>
/// Password hashing port. The BCrypt adapter lives in Infrastructure/Identity
/// (added with the Auth feature); plaintext passwords never leave this seam.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
