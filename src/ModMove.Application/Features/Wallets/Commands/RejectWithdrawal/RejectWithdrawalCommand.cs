using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Wallets.Commands.RejectWithdrawal;

/// <summary>
/// UC-ADM-07, GB-27, DECISIONS 5.1: Admin rejects a pending driver earnings withdrawal and refunds the held amount to EarningsBalance.
/// </summary>
public sealed record RejectWithdrawalCommand(long TransactionId, string? Reason = null) : ICommand;

public sealed record RejectWithdrawalRequest(string? Reason = null);

