using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Trips.Commands.ConfirmPickup;

public sealed record ConfirmPickupRequest(IReadOnlyList<string> PhotoUrls);

/// <summary>
/// UC-DRV-13, GB-11, DECISIONS 6.3: the assigned driver uploads 2..5 pickup photos (Arrived -> PickedUp),
/// or resubmits new pickup photos after customer rejection (at most 5 submissions in all).
/// </summary>
public sealed record ConfirmPickupCommand(long OrderId, IReadOnlyList<string> PhotoUrls) : ICommand;

