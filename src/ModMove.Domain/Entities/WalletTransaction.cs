using ModMove.Domain.Common;
using ModMove.Domain.Enums;

namespace ModMove.Domain.Entities;

/// <summary>Every balance movement in a driver wallet. Settled rows are never edited (earnings immutability).</summary>
public sealed class WalletTransaction : BaseAuditableEntity
{
    private WalletTransaction() { }

    public long WalletId { get; private set; }

    /// <summary>Unique together with <see cref="TransactionType"/>: an order settles once.</summary>
    public long? OrderId { get; private set; }

    public WalletTransactionType TransactionType { get; private set; }
    public decimal Amount { get; private set; }
    public decimal BalanceAfter { get; private set; }
    public WalletTransactionStatus TransactionStatus { get; private set; }

    /// <summary>Bank reference of the manual payout (GB-27).</summary>
    public string? ReferenceCode { get; private set; }

    public long? ProcessedBy { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    internal static WalletTransaction Create(
        long walletId, WalletTransactionType type, long? orderId, decimal amount, decimal balanceAfter, WalletTransactionStatus status) =>
        new()
        {
            WalletId = walletId,
            TransactionType = type,
            OrderId = orderId,
            Amount = amount,
            BalanceAfter = balanceAfter,
            TransactionStatus = status,
        };

    internal void Complete(long adminId, string referenceCode, DateTimeOffset now)
    {
        TransactionStatus = WalletTransactionStatus.Completed;
        ReferenceCode = referenceCode;
        ProcessedBy = adminId;
        ProcessedAt = now;
    }

    internal void Reject(long adminId, DateTimeOffset now)
    {
        TransactionStatus = WalletTransactionStatus.Rejected;
        ProcessedBy = adminId;
        ProcessedAt = now;
    }
}
