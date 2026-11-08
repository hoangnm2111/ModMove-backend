using ModMove.Application.Common.Messaging;
using ModMove.Application.Features.Incidents.Commands.ReportIncidentByCustomer;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Incidents.Commands.ReportIncidentByDriver;

/// <summary>
/// UC-DRV-18, DECISIONS 6.1, 7.1: a driver reports an incident on their active order (Accepted..InTransit).
/// "Late", "wrong route", "customer absent at pickup" use <see cref="IncidentType.Other"/> (7.1).
/// </summary>
public sealed record ReportIncidentByDriverCommand(
    long OrderId,
    IncidentType IncidentType,
    string Description,
    IReadOnlyList<string>? PhotoUrls = null,
    decimal? Latitude = null,
    decimal? Longitude = null) : ICommand<IncidentCreatedResponse>;

