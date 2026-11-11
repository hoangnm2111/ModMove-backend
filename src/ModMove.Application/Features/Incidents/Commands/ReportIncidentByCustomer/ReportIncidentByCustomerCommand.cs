using ModMove.Application.Common.Messaging;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Incidents.Commands.ReportIncidentByCustomer;

/// <summary>
/// UC-CUS-15: the customer reports a problem while the driver has the order (Arrived, PickedUp, InTransit; DECISIONS 2.2).
/// "Late", "wrong route" and similar go in as Other (7.1). Photo URLs come from POST api/customer/uploads.
/// Unlike a driver's report it does not hold the trip (7.6); the driver only gets a generic notice (8.3).
/// </summary>
public sealed record ReportIncidentByCustomerCommand(
    long OrderId, IncidentType IncidentType, string Description, IReadOnlyList<string>? PhotoUrls,
    decimal? Latitude, decimal? Longitude) : ICommand<IncidentCreatedResponse>;

public sealed record IncidentCreatedResponse(long IncidentId);
