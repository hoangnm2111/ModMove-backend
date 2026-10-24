using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Auth.Commands.Logout;

/// <summary>
/// UC-CUS-03 / UC-DRV-02. Revokes every refresh token of the user (DECISIONS 9.1) and clears the FCM token, so
/// pushes stop. The access token is not stored server-side: the client drops it and it expires within 15 minutes.
/// </summary>
public sealed record LogoutCommand : ICommand;
