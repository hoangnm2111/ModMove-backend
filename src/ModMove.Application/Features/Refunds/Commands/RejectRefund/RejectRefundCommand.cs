using ModMove.Application.Common.Messaging;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Refunds.Commands.RejectRefund;

/// <summary>UC-ADM-19 (DECISIONS CUS-23, GB-19): an Administrator rejects a refund request or flags a wrong bank account.</summary>
public sealed record RejectRefundCommand(
    long RefundId,
    string? Note = null,
    PayoutRejectionReasonCode? ReasonCode = null) : ICommand;

public sealed record RejectRefundRequest(
    string? Note = null,
    PayoutRejectionReasonCode? ReasonCode = null,
    string? Reason = null);

