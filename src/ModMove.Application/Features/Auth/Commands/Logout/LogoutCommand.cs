using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Auth.Commands.Logout;

/// <summary>
/// UC-CUS-03 / UC-DRV-02. Revokes every refresh token of the user (DECISIONS 9.1). The app sends its own
/// <see cref="FcmToken"/>: pushes stop only if it is still the token on file, so signing out on an old phone keeps the
/// newer phone's pushes (9.2). The web sends none (no FCM on the web). The access token is not stored server-side:
/// the client drops it and it expires within 15 minutes.
/// </summary>
public sealed record LogoutCommand(string? FcmToken = null) : ICommand;
