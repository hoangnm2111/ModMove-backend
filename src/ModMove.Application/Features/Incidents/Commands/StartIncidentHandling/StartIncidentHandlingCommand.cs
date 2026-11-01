using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Incidents.Commands.StartIncidentHandling;

/// <summary>UC-DIS-05: claim an Open incident for handling (transitions to InProgress).</summary>
public sealed record StartIncidentHandlingCommand(long IncidentId) : ICommand;

