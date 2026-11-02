using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Wallets.Commands.ApproveWithdrawal;

/// <summary>
/// UC-ADM-07, GB-27: Admin approves a pending driver earnings withdrawal and records the manual bank transfer reference code.
/// </summary>
public sealed record ApproveWithdrawalCommand(long TransactionId, string BankReferenceCode) : ICommand;

public sealed record ApproveWithdrawalRequest(string BankReferenceCode);

