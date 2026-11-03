using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Matching.Commands.CancelUnmatchedOrders;

/// <summary>
/// JOB-MATCH-01 (GB-24, GB-41, DECISIONS 3.2, 3.3, B9, 8.3): cancels Pending orders that have waited 10 minutes
/// without a driver (from <c>created_at</c> for Cash, from <c>paid_at</c> for Paid PayOS), creates a 100%
/// Cancellation refund for Paid PayOS orders, and notifies the customers. Returns how many orders were cancelled.
/// </summary>
public sealed record CancelUnmatchedOrdersCommand : ICommand<int>;

