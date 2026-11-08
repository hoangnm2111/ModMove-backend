using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Trips.Commands.NotifyArrivalAtDropoff;

/// <summary>
/// UC-DRV-15: notifies the customer that the driver has arrived at the drop-off destination while the order is <c>InTransit</c>
/// (no order status change).
/// </summary>
public sealed record NotifyArrivalAtDropoffCommand(long OrderId) : ICommand;

