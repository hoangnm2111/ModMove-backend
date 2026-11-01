using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Refunds.Commands.ProposeComplaintResolution;

/// <summary>
/// UC-DIS-15 / UC-DIS-09 / UC-DIS-17: the Dispatcher proposes full, partial, or no refund (amount 0) with a justification,
/// sending the proposal to the Administrator for approval.
/// </summary>
public sealed record ProposeComplaintResolutionCommand(long ComplaintId, decimal Amount, string Reason) : ICommand;
