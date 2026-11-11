using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Refunds.Commands.SetRefundPayoutAccount;

/// <summary>
/// R-01: the customer gives, or fixes, the bank account that receives a refund. Allowed while the refund is
/// Requested, Approved or Failed; a Failed refund goes back to Approved so the Administrator can pay again.
/// </summary>
public sealed record SetRefundPayoutAccountCommand(long RefundId, PayoutAccountInput PayoutAccount) : ICommand;
