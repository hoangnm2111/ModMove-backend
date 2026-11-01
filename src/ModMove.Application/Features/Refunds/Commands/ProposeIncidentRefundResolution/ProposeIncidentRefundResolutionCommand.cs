using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Refunds.Commands.ProposeIncidentRefundResolution;

/// <summary>
/// UC-DIS-09 / UC-DIS-17: the Dispatcher proposes a full, partial or no refund (amount 0) for the refund requested
/// because of an incident; the Administrator approves it later (GB-19).
/// </summary>
public sealed record ProposeIncidentRefundResolutionCommand(long IncidentId, decimal Amount, string Reason) : ICommand;

