using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Refunds.Commands.RejectRefund;

/// <summary>UC-ADM-19 (DECISIONS CUS-23, GB-19): an Administrator rejects a refund request.</summary>
public sealed record RejectRefundCommand(long RefundId, string? Note = null) : ICommand;

public sealed record RejectRefundRequest(string? Note = null);

