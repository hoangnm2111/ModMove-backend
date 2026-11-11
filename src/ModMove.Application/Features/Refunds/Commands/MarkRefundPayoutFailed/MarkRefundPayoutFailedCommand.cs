using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Refunds.Commands.MarkRefundPayoutFailed;

/// <summary>UC-ADM-18 5a (R-01): an Administrator records that the manual bank transfer failed so the customer fixes the account.</summary>
public sealed record MarkRefundPayoutFailedCommand(long RefundId, string Reason) : ICommand;

public sealed record MarkRefundPayoutFailedRequest(string Reason);

