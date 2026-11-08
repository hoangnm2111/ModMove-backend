using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Trips.Commands.ConfirmArrival;

/// <summary>UC-DRV-12: the assigned driver confirms arriving at the pickup (or handover) point (Accepted -> Arrived).</summary>
public sealed record ConfirmArrivalCommand(long OrderId) : ICommand;

