using ModMove.Domain.Enums;

namespace ModMove.Application.Common.Interfaces;

public interface IJwtTokenService
{
    /// <summary>Signed access token carrying the user id (NameIdentifier) and role claims.</summary>
    string CreateAccessToken(long userId, UserRole role);
}
