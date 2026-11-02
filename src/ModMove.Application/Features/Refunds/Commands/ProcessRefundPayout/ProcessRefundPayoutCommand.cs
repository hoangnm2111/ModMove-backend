using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Refunds.Commands.ProcessRefundPayout;

/// <summary>UC-ADM-20 (GB-27): an Administrator records the bank transfer reference code after manually paying out an approved refund.</summary>
public sealed record ProcessRefundPayoutCommand(long RefundId, string BankReferenceCode) : ICommand;

public sealed record ProcessRefundPayoutRequest(string BankReferenceCode);

