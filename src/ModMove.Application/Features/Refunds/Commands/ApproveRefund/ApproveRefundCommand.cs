using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Refunds.Commands.ApproveRefund;

/// <summary>
/// UC-ADM-19 (DECISIONS B9, 3.3, CUS-23, GB-19): an Administrator approves a refund request (a cancellation refund goes
/// straight here; an incident or complaint refund comes after the Dispatcher's proposal).
/// </summary>
public sealed record ApproveRefundCommand(long RefundId, string? Note = null) : ICommand;

public sealed record ApproveRefundRequest(string? Note = null);

