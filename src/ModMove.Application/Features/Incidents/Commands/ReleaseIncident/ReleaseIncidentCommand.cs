using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Incidents.Commands.ReleaseIncident;

/// <summary>DECISIONS Q10: the Dispatcher handling an incident gives it back (InProgress → Open) for another Dispatcher.</summary>
public sealed record ReleaseIncidentCommand(long IncidentId) : ICommand;
