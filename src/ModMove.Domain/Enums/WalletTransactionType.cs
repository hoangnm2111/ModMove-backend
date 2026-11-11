namespace ModMove.Domain.Enums;

public enum WalletTransactionType
{
    TopUpDeposit,
    TripEarning,
    CommissionDeduction,

    /// <summary>UC-DRV-22: a withdrawal taken from EarningsBalance first and from DepositBalance for the remainder (GB-32).</summary>
    Withdrawal,

    /// <summary>GB-32: money moved automatically from EarningsBalance to DepositBalance up to REQUIRED_DEPOSIT.</summary>
    DepositRefill,
}
