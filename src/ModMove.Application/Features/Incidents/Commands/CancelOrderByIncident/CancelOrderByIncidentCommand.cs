using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Incidents.Commands.CancelOrderByIncident;

/// <summary>DECISIONS 2.6: what the driver does with cargo still on the vehicle when an order is cancelled mid-trip.</summary>
public enum CargoDisposition
{
    ReturnToPickup,
    Keep,
}

/// <summary>
/// UC-DIS-07 (DECISIONS 2.4, 2.5, 2.6, 4.3): cancel an active order due to an incident, optionally creating a 100%
/// cancellation refund for a paid PayOS order and recording cargo disposition when goods are already on the vehicle.
/// </summary>
public sealed record CancelOrderByIncidentCommand(
    long IncidentId,
    string Reason,
    bool Refund,
    CargoDisposition? CargoDisposition = null) : ICommand;

