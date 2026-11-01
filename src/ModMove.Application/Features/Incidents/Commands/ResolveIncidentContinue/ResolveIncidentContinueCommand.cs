using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Incidents.Commands.ResolveIncidentContinue;

/// <summary>UC-DIS-05: resolve an incident by instructing the assigned driver to continue the trip.</summary>
public sealed record ResolveIncidentContinueCommand(long IncidentId, string Reason) : ICommand;

