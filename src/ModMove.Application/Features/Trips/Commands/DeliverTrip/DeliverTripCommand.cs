using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Trips.Commands.DeliverTrip;

public sealed record DeliverTripRequest(
    string DeliveryPhotoUrl,
    bool CashCollected = false,
    decimal? Latitude = null,
    decimal? Longitude = null);

/// <summary>
/// UC-DRV-16, GB-12, DECISIONS 2.7, 6.2: the assigned driver uploads a proof-of-delivery photo to complete delivery
/// (InTransit -> Delivered). For an unpaid Cash order, cash must either already be collected via CollectCash or
/// confirmed inline via <paramref name="CashCollected"/> with valid location.
/// </summary>
public sealed record DeliverTripCommand(
    long OrderId,
    string DeliveryPhotoUrl,
    bool CashCollected = false,
    decimal? Latitude = null,
    decimal? Longitude = null) : ICommand;

