using ModMove.Domain.Common;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.Domain.Entities;

/// <summary>Deposit and earnings balances of a driver; root of its transactions (GB-32).</summary>
public sealed class DriverWallet : BaseAuditableEntity, IAggregateRoot
{
    private readonly List<WalletTransaction> _transactions = [];

    private DriverWallet() { }

    public long DriverProfileId { get; private set; }

    /// <summary>At least REQUIRED_DEPOSIT (default 200,000 VND) to receive any order (GB-16, GB-32).</summary>
    public decimal DepositBalance { get; private set; }

    /// <summary>The driver's net earnings.</summary>
    public decimal EarningsBalance { get; private set; }

    public WalletStatus WalletStatus { get; private set; } = WalletStatus.Active;

    /// <summary>GB-32: wallet total = DepositBalance + EarningsBalance.</summary>
    public decimal WalletTotal => DepositBalance + EarningsBalance;

    /// <summary>Optimistic lock on balance changes.</summary>
    public int RowVersion { get; private set; }

    public IReadOnlyCollection<WalletTransaction> Transactions => _transactions;

    public static DriverWallet Create(long driverProfileId) => new() { DriverProfileId = driverProfileId };

    /// <summary>GB-32: the part of the withdrawable total the driver can take out now; 0 for a frozen wallet.</summary>
    /// <param name="hasActiveOrder">While the driver has an active order only EarningsBalance can be withdrawn (GB-32).</param>
    public decimal GetMaxWithdrawable(bool hasActiveOrder = false)
    {
        if (WalletStatus != WalletStatus.Active)
            return 0m;

        return hasActiveOrder
            ? Math.Max(0m, Math.Min(EarningsBalance, WalletTotal))
            : Math.Max(0m, WalletTotal);
    }

    /// <summary>GB-16: the deposit must reach <paramref name="requiredDeposit"/> (REQUIRED_DEPOSIT) to receive any order, cash or PayOS.</summary>
    public bool CanReceiveOrders(decimal requiredDeposit) =>
        WalletStatus == WalletStatus.Active && DepositBalance >= requiredDeposit;

    /// <summary>
    /// GB-16: for a cash order the deposit must also cover this order's commission (<see cref="ShippingOrder.CommissionFor"/>).
    /// The wallet only decides whether a driver may take an order, never whether a finished one completes. The same check applies
    /// to a driver taking over the order (DECISIONS 7.2).
    /// </summary>
    public bool CanReceiveCod(decimal requiredDeposit, decimal orderCommission) =>
        CanReceiveOrders(requiredDeposit) && DepositBalance >= orderCommission;

    /// <summary>GB-32, UC-DRV-21: how much the deposit is short of <paramref name="requiredDeposit"/> (0 when it is enough).</summary>
    public decimal MissingDeposit(decimal requiredDeposit) => Math.Max(0m, requiredDeposit - DepositBalance);

    /// <summary>
    /// GB-32, UC-DRV-21: the wallet must be Active and the amount above 0. While the deposit is below REQUIRED_DEPOSIT the amount must
    /// at least cover the missing part; above it any amount is allowed (a larger deposit takes cash orders with a larger commission).
    /// </summary>
    public void EnsureCanTopUpDeposit(decimal amount, decimal requiredDeposit = 200_000m)
    {
        EnsureActive();
        EnsurePositive(amount);
        var missing = MissingDeposit(requiredDeposit);
        if (amount < missing)
            throw new DomainException(
                $"The deposit top-up must be at least the missing amount of {missing:N0} VND.", "TOP_UP_BELOW_MISSING_DEPOSIT");
    }

    /// <summary>UC-DRV-21: used when the top-up is requested; the amount follows <see cref="EnsureCanTopUpDeposit"/>.</summary>
    public WalletTransaction TopUpDeposit(decimal amount, decimal requiredDeposit = 200_000m)
    {
        EnsureCanTopUpDeposit(amount, requiredDeposit);
        return CreditTopUp(amount);
    }

    /// <summary>
    /// The signed PayOS webhook confirmed a top-up: the money is already received, so it is credited whatever the wallet looks like
    /// now (the rules were checked when the payment link was created).
    /// </summary>
    public WalletTransaction CreditTopUp(decimal amount)
    {
        EnsurePositive(amount);
        DepositBalance += amount;
        return Record(WalletTransactionType.TopUpDeposit, null, amount, DepositBalance, WalletTransactionStatus.Completed, 0m, amount);
    }

    /// <summary>Online-paid trip: the driver earns the fare minus the platform commission (GB-14). The trip is done, so a frozen wallet still settles.</summary>
    public WalletTransaction CreditTripEarning(long orderId, decimal fare, decimal commission)
    {
        EnsurePositive(fare);
        if (commission < 0 || commission > fare)
            throw new DomainException("Commission must be between 0 and the fare.");

        EarningsBalance += fare - commission;
        return Record(WalletTransactionType.TripEarning, orderId, fare - commission, EarningsBalance, WalletTransactionStatus.Completed,
            fare - commission, 0m);
    }

    /// <summary>
    /// Cash trip: commission is taken from the deposit (GB-15). The customer must never be blocked by the driver's wallet, so
    /// the deposit may go negative; <see cref="RefillDepositFromEarnings"/> then covers it from earnings, and the driver cannot take orders
    /// until the deposit is back to REQUIRED_DEPOSIT (GB-16).
    /// </summary>
    public WalletTransaction DeductCommissionFromDeposit(long orderId, decimal commission)
    {
        EnsurePositive(commission);

        DepositBalance -= commission;
        return Record(WalletTransactionType.CommissionDeduction, orderId, commission, DepositBalance, WalletTransactionStatus.Completed,
            0m, commission);
    }

    /// <summary>
    /// GB-32: whenever the deposit is below <paramref name="requiredDeposit"/> and earnings are above 0, money moves from earnings to the
    /// deposit until it reaches REQUIRED_DEPOSIT or the earnings are used up. Runs in the same transaction as the change that caused
    /// it (an order settling, a top-up, a rejected withdrawal). A frozen wallet still refills. Returns null when nothing moved.
    /// </summary>
    public WalletTransaction? RefillDepositFromEarnings(decimal requiredDeposit = 200_000m)
    {
        if (EarningsBalance <= 0 || DepositBalance >= requiredDeposit)
            return null;

        var moved = Math.Min(EarningsBalance, requiredDeposit - DepositBalance);
        EarningsBalance -= moved;
        DepositBalance += moved;
        return Record(WalletTransactionType.DepositRefill, null, moved, DepositBalance, WalletTransactionStatus.Completed, moved, moved);
    }

    /// <summary>
    /// UC-DRV-22, GB-32: the amount is held at once, from earnings first and from the deposit for the remainder, both parts recorded
    /// on the transaction; an Administrator pays it out manually (GB-27). Up to the wallet total, or earnings only while the driver has
    /// an active order. No minimum, one Pending request at a time.
    /// </summary>
    public WalletTransaction RequestWithdrawal(decimal amount, bool hasActiveOrder = false)
    {
        EnsureActive();
        EnsurePositive(amount);
        if (_transactions.Any(t => t.TransactionType == WalletTransactionType.Withdrawal && t.TransactionStatus == WalletTransactionStatus.Pending))
            throw new DomainException("A withdrawal request is already pending review.", "WITHDRAWAL_ALREADY_PENDING");

        if (amount > GetMaxWithdrawable(hasActiveOrder))
            throw new DomainException(
                hasActiveOrder
                    ? "The amount exceeds your earnings; only earnings can be withdrawn while you have an active order."
                    : "The amount exceeds your withdrawable balance.",
                "WITHDRAWAL_EXCEEDS_BALANCE");

        var fromEarnings = Math.Min(amount, Math.Max(0m, EarningsBalance));
        var fromDeposit = amount - fromEarnings;
        EarningsBalance -= fromEarnings;
        DepositBalance -= fromDeposit;
        return Record(WalletTransactionType.Withdrawal, null, amount, WalletTotal, WalletTransactionStatus.Pending, fromEarnings, fromDeposit);
    }

    public void CompleteWithdrawal(WalletTransaction withdrawal, long adminId, string referenceCode, DateTimeOffset now)
    {
        EnsurePendingWithdrawal(withdrawal);
        withdrawal.Complete(adminId, referenceCode, now);
    }

    /// <summary>
    /// UC-ADM-24, GB-32: each held part goes back to the balance it came from (never all into earnings), then the deposit is refilled
    /// if it ended up below <paramref name="requiredDeposit"/> while earnings are above 0.
    /// </summary>
    public void RejectWithdrawal(WalletTransaction withdrawal, long adminId, DateTimeOffset now, decimal requiredDeposit = 200_000m)
    {
        EnsurePendingWithdrawal(withdrawal);
        EarningsBalance += withdrawal.EarningsAmount;
        DepositBalance += withdrawal.DepositAmount;
        withdrawal.Reject(adminId, now);
        RowVersion++;
        RefillDepositFromEarnings(requiredDeposit);
    }

    private WalletTransaction Record(
        WalletTransactionType type, long? orderId, decimal amount, decimal balanceAfter, WalletTransactionStatus status,
        decimal earningsAmount, decimal depositAmount)
    {
        RowVersion++;
        var tx = WalletTransaction.Create(Id, type, orderId, amount, balanceAfter, status, earningsAmount, depositAmount);
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

        if (tx.TransactionType != WalletTransactionType.Withdrawal || tx.TransactionStatus != WalletTransactionStatus.Pending)
            throw new DomainException("Only a pending withdrawal can be processed.");
    }
}
