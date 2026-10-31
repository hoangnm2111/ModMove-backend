using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Refunds.Commands.WithdrawRefund;

/// <summary>UC-CUS-24: the customer drops a complaint or incident refund still Requested; a cancellation refund stays (B9).</summary>
public sealed record WithdrawRefundCommand(long RefundId) : ICommand;
