using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Trips.Commands.CollectCash;

public sealed record CollectCashRequest(decimal? Latitude, decimal? Longitude);

/// <summary>
/// UC-DRV-17: the assigned driver confirms collecting cash for a Cash order at the allowed step (GB-52: no server location check),
/// recording a Paid cash PaymentTransaction and marking the order Paid.
/// </summary>
public sealed record CollectCashCommand(long OrderId, decimal? Latitude, decimal? Longitude) : ICommand;

