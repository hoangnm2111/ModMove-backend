using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Tracking.Queries.CanFollowOrder;

/// <summary>
/// DECISIONS B12, C10: JoinOrder lets an Active customer follow only their own order. The driver does not join order
/// groups; status changes reach the driver through Clients.User. The ids come from the token and the hub call.
/// </summary>
public sealed record CanFollowOrderQuery(long UserId, long OrderId) : IQuery<bool>;
