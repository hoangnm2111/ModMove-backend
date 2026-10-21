using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Auth;

internal static class CurrentUser
{
    /// <summary>The signed-in user's aggregate. A valid token whose user no longer exists is treated as signed out.</summary>
    public static async Task<User> LoadAsync(ICurrentUserService currentUser, IUserRepository users, CancellationToken ct)
    {
        var id = currentUser.UserId ?? throw new UnauthorizedException("Sign in again.");
        return await users.GetByIdAsync(id, ct) ?? throw new UnauthorizedException("Sign in again.");
    }
}
