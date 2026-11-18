using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Auth.Commands.PurgeExpiredRefreshTokens;

/// <summary>JOB-AUTH-01: deletes refresh tokens past their 30-day expiry. Returns how many went.</summary>
public sealed record PurgeExpiredRefreshTokensCommand : ICommand<int>;
