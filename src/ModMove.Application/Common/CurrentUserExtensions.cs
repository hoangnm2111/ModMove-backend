using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Application.Common;

internal static class CurrentUserExtensions
{
    /// <summary>The signed-in user's id; no id means the caller is not signed in.</summary>
    public static long RequireUserId(this ICurrentUserService currentUser) =>
        currentUser.UserId ?? throw new UnauthorizedException("Sign in again.");

    /// <summary>
    /// The signed-in user's aggregate. A token outlives a deletion or an Admin lock by up to 15 minutes,
    /// so a deactivated or locked account is refused here even though its token is still valid.
    /// </summary>
    public static async Task<User> LoadUserAsync(this ICurrentUserService currentUser, IUserRepository users, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(currentUser.RequireUserId(), ct) ?? throw new UnauthorizedException("Sign in again.");

        if (user.AccountStatus != AccountStatus.Active)
            throw new ForbiddenAccessException("The account is not active.", "ACCOUNT_INACTIVE");

        return user;
    }
}
