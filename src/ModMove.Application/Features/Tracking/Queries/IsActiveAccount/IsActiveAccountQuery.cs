using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Tracking.Queries.IsActiveAccount;

/// <summary>DECISIONS B12, B8: /hubs/tracking keeps a connection only for an Active account; the id comes from the token.</summary>
public sealed record IsActiveAccountQuery(long UserId) : IQuery<bool>;
