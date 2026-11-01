using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Incidents.Commands.CancelIncident;

/// <summary>UC-DIS-10: dismiss an incident report that has no valid basis; the order continues normally.</summary>
public sealed record CancelIncidentCommand(long IncidentId, string Reason) : ICommand;

