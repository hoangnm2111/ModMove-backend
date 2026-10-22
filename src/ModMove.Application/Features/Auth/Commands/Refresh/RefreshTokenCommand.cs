using ModMove.Application.Common.Messaging;
using ModMove.Application.Features.Auth.Commands.Login;

namespace ModMove.Application.Features.Auth.Commands.Refresh;

/// <summary>Swap a refresh token for a new access token and a new refresh token (the old one stops working).</summary>
public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<LoginResponse>;
