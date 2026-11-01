using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Incidents.Commands.ReassignIncidentDriver;

/// <summary>UC-DIS-06 (DECISIONS 7.2): reassign an active order with an incident to an eligible replacement driver.</summary>
public sealed record ReassignIncidentDriverCommand(long IncidentId, long NewDriverProfileId, string Reason) : ICommand;

