using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Users.Commands.UpdateFcmToken;

/// <summary>The mobile app registers its Firebase device token after sign-in, so pushes reach this device.</summary>
public sealed record UpdateFcmTokenCommand(string Token) : ICommand;
