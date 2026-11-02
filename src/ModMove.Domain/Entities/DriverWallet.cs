using ModMove.Domain.Constants;
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

    /// <summary>
    /// GB-16: the deposit covers both <paramref name="minDeposit"/> (the MIN_COD_DEPOSIT setting) and the commission of
    /// this order (<see cref="ShippingOrder.CommissionFor"/>). The wallet only decides whether a driver may take a cash
    /// order, never whether a finished one completes. The same check applies to a driver taking over the order (DECISIONS 7.2).
    /// </summary>
    public bool CanReceiveCod(decimal minDeposit, decimal orderCommission) =>
        WalletStatus == WalletStatus.Active && DepositBalance >= minDeposit && DepositBalance >= orderCommission;

    /// <summary>DECISIONS 5.2: at least 50,000 VND per top-up. Withdrawals have no minimum (5.1).</summary>
    public WalletTransaction TopUpDeposit(decimal amount)
    {
        EnsureActive();
        if (amount < BusinessRules.MinDepositTopUp)
            throw new DomainException($"A deposit top-up must be at least {BusinessRules.MinDepositTopUp:N0} VND.", "TOP_UP_TOO_SMALL");
        DepositBalance += amount;
        return Record(WalletTransactionType.TopUpDeposit, null, amount, DepositBalance, WalletTransactionStatus.Completed);
    }

    /// <summary>Online-paid trip: the driver earns the fare minus the platform commission (GB-14). The trip is done, so a frozen wallet still settles.</summary>
    public WalletTransaction CreditTripEarning(long orderId, decimal fare, decimal commission)
    {
        EnsurePositive(fare);
        if (commission < 0 || commission > fare)
            throw new DomainException("Commission must be between 0 and the fare.");

        EarningsBalance += fare - commission;
        return Record(WalletTransactionType.TripEarning, orderId, fare - commission, EarningsBalance, WalletTransactionStatus.Completed);
    }

    /// <summary>
    /// Cash trip: commission is taken from the deposit (GB-15). The customer must never be blocked by the driver's wallet, so
    /// the deposit may go negative; the driver then cannot take cash orders until topping up (GB-16).
    /// </summary>
    public WalletTransaction DeductCommissionFromDeposit(long orderId, decimal commission)
    {
        EnsurePositive(commission);

        DepositBalance -= commission;
        return Record(WalletTransactionType.CommissionDeduction, orderId, commission, DepositBalance, WalletTransactionStatus.Completed);
    }

    /// <summary>
    /// UC-DRV-22: the amount is held at once; an Administrator pays it out manually (GB-27). Commission owed on a negative
    /// deposit is paid from earnings first (5.1). A rejected withdrawal gives back the amount only; the debt stays paid.
    /// </summary>
    public WalletTransaction RequestWithdrawal(decimal amount)
    {
        EnsureActive();
        EnsurePositive(amount);
        var debt = Math.Max(0, -DepositBalance);
        if (EarningsBalance - debt < amount)
            throw new DomainException("The earnings balance, after the commission owed on the deposit, is not enough for this withdrawal.");

        if (debt > 0)
        {
            EarningsBalance -= debt;
            DepositBalance = 0;
            Record(WalletTransactionType.CommissionDebtPaid, null, debt, DepositBalance, WalletTransactionStatus.Completed);
        }

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
