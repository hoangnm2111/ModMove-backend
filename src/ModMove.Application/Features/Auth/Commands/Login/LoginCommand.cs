using ModMove.Application.Common.Messaging;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Auth.Commands.Login;

public sealed record LoginCommand(string Email, string Password) : ICommand<LoginResponse>;

/// <summary>
/// <see cref="AccessToken"/> lasts 15 minutes; when it expires the client calls api/public/auth/refresh with
/// <see cref="RefreshToken"/> (30 days, single use) to get a new pair (DECISIONS 9.1).
/// </summary>
public sealed record LoginResponse(string AccessToken, string RefreshToken, long UserId, string FullName, UserRole Role);
