using ModMove.Application.Common.Messaging;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Users.Commands.UpdateUserStatus;

/// <summary>
/// UC-ADM-06 (DECISIONS B8, 8.2): activate, deactivate, lock, or unlock a user account with a required reason.
/// Setting Inactive or Locked revokes all active sessions (<c>RevokeAllForUserAsync</c>) and clears the FCM token.
/// </summary>
public sealed record UpdateUserStatusCommand(
    long UserId,
    AccountStatus NewStatus,
    string Reason) : ICommand;

