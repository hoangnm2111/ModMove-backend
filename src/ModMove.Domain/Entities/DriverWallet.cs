using ModMove.Domain.Common;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.Domain.Entities;

/// <summary>Deposit and earnings balances of a driver; root of its transactions.</summary>
public sealed class DriverWallet : BaseAuditableEntity, IAggregateRoot
{
    private readonly List<WalletTransaction> _transactions = [];

    private DriverWallet() { }

    public long DriverProfileId { get; private set; }

    /// <summary>At least 200,000 VND to receive COD orders (GB-16).</summary>
    public decimal DepositBalance { get; private set; }

    /// <summary>Withdrawable.</summary>
    public decimal EarningsBalance { get; private set; }

    public WalletStatus WalletStatus { get; private set; } = WalletStatus.Active;

    /// <summary>Optimistic lock on balance changes.</summary>
    public int RowVersion { get; private set; }

    public IReadOnlyCollection<WalletTransaction> Transactions => _transactions;

    public static DriverWallet Create(long driverProfileId) => new() { DriverProfileId = driverProfileId };

    /// <summary>GB-16: <paramref name="minDeposit"/> is the MIN_COD_DEPOSIT platform setting (Admin can change it).</summary>
    public bool CanReceiveCod(decimal minDeposit) => WalletStatus == WalletStatus.Active && DepositBalance >= minDeposit;

    public WalletTransaction TopUpDeposit(decimal amount)
    {
        EnsureActive();
        EnsurePositive(amount);
        DepositBalance += amount;
        return Record(WalletTransactionType.TopUpDeposit, null, amount, DepositBalance, WalletTransactionStatus.Completed);
    }

    /// <summary>Online-paid trip: the driver earns the fare minus the platform commission (GB-14).</summary>
    public WalletTransaction CreditTripEarning(long orderId, decimal fare, decimal commission)
    {
        EnsureActive();
        EnsurePositive(fare);
        if (commission < 0 || commission > fare)
            throw new DomainException("Commission must be between 0 and the fare.");

        EarningsBalance += fare - commission;
        return Record(WalletTransactionType.TripEarning, orderId, fare - commission, EarningsBalance, WalletTransactionStatus.Completed);
    }

    /// <summary>Cash trip: commission is taken from the deposit (GB-15).</summary>
    public WalletTransaction DeductCommissionFromDeposit(long orderId, decimal commission)
    {
        EnsureActive();
        EnsurePositive(commission);
        if (DepositBalance < commission)
            throw new DomainException("The deposit is not enough to cover the commission.");

        DepositBalance -= commission;
        return Record(WalletTransactionType.CommissionDeduction, orderId, commission, DepositBalance, WalletTransactionStatus.Completed);
    }

    /// <summary>UC-DRV-22: the amount is held at once; an Administrator pays it out manually (GB-27).</summary>
    public WalletTransaction RequestWithdrawal(decimal amount)
    {
        EnsureActive();
        EnsurePositive(amount);
        if (EarningsBalance < amount)
            throw new DomainException("The earnings balance is not enough for this withdrawal.");

        EarningsBalance -= amount;
        return Record(WalletTransactionType.WithdrawEarnings, null, amount, EarningsBalance, WalletTransactionStatus.Pending);
    }

    public void CompleteWithdrawal(WalletTransaction withdrawal, long adminId, string referenceCode, DateTimeOffset now)
    {
        EnsurePendingWithdrawal(withdrawal);
        withdrawal.Complete(adminId, referenceCode, now);
    }

    public void RejectWithdrawal(WalletTransaction withdrawal, long adminId, DateTimeOffset now)
    {
        EnsurePendingWithdrawal(withdrawal);
        EarningsBalance += withdrawal.Amount;
        withdrawal.Reject(adminId, now);
        RowVersion++;
    }

    private WalletTransaction Record(
        WalletTransactionType type, long? orderId, decimal amount, decimal balanceAfter, WalletTransactionStatus status)
    {
        RowVersion++;
        var tx = WalletTransaction.Create(type, orderId, amount, balanceAfter, status);
        _transactions.Add(tx);
        return tx;
    }

    private void EnsureActive()
    {
        if (WalletStatus != WalletStatus.Active)
            throw new DomainException("The wallet is frozen.");
    }

    private static void EnsurePositive(decimal amount)
    {
        if (amount <= 0)
            throw new DomainException("The amount must be positive.");
    }

    private void EnsurePendingWithdrawal(WalletTransaction tx)
    {
        if (tx.WalletId != Id)
            throw new DomainException("The transaction does not belong to this wallet.");

        if (tx.TransactionType != WalletTransactionType.WithdrawEarnings || tx.TransactionStatus != WalletTransactionStatus.Pending)
            throw new DomainException("Only a pending withdrawal can be processed.");
    }
}
