using ModMove.Application.Common.Messaging;
using ModMove.Application.Features.Incidents.Commands.ReportIncidentByCustomer;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Incidents.Commands.ReportIncidentByDispatcher;

/// <summary>
/// DECISIONS Q6b: a Dispatcher opens an incident on a running order to reassign the driver or cancel the order when nobody
/// reported anything (a driver who stopped answering, an Administrator who must lock a driver). The description is an
/// internal note: the customer and the driver only get the generic "being handled" notice (8.3).
/// </summary>
public sealed record ReportIncidentByDispatcherCommand(long OrderId, IncidentType IncidentType, string Description)
    : ICommand<IncidentCreatedResponse>;

/// <summary>Body of POST api/dispatcher/orders/{id}/incidents.</summary>
public sealed record ReportIncidentByDispatcherBody(IncidentType IncidentType, string Description);
