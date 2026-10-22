using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Auth.Commands.Logout;

/// <summary>
/// UC-CUS-03 / UC-DRV-02. JWTs are not stored server-side, so the client drops its token; the server stops
/// sending push notifications to this device by clearing the FCM token.
/// </summary>
public sealed record LogoutCommand : ICommand;
