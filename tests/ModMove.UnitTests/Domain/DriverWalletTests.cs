using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.UnitTests.Domain;

public class DriverWalletTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Cod_needs_a_deposit_of_at_least_200000_GB16()
    {
        const decimal minDeposit = 200_000; // seeded MIN_COD_DEPOSIT
        const decimal commission = 18_000; // 10% of a 180,000 order
        var wallet = DriverWallet.Create(1);
        Assert.False(wallet.CanReceiveCod(minDeposit, commission));

        wallet.TopUpDeposit(149_999);
        wallet.TopUpDeposit(50_000); // 199,999
        Assert.False(wallet.CanReceiveCod(minDeposit, commission));

        wallet.TopUpDeposit(50_000);
        Assert.True(wallet.CanReceiveCod(minDeposit, commission));
    }

    [Fact]
    public void Cod_also_needs_a_deposit_that_covers_the_order_commission_GB16()
    {
        var wallet = DriverWallet.Create(1);
        wallet.TopUpDeposit(200_000);

        Assert.True(wallet.CanReceiveCod(minDeposit: 200_000, orderCommission: 200_000));
        Assert.False(wallet.CanReceiveCod(minDeposit: 200_000, orderCommission: 250_000)); // 10% of a 2,500,000 order
    }

    [Fact]
    public void Trip_earning_is_fare_minus_commission_GB14()
    {
        var wallet = DriverWallet.Create(1);

        var tx = wallet.CreditTripEarning(orderId: 10, fare: 200_000, commission: 20_000);

        Assert.Equal(180_000m, wallet.EarningsBalance);
        Assert.Equal(180_000m, tx.BalanceAfter);
    }

    [Fact]
    public void Cash_commission_is_taken_from_deposit_GB15()
    {
        var wallet = DriverWallet.Create(1);
        wallet.TopUpDeposit(300_000);

        wallet.DeductCommissionFromDeposit(orderId: 10, commission: 20_000);

        Assert.Equal(280_000m, wallet.DepositBalance);
    }

    [Fact]
    public void Commission_larger_than_deposit_leaves_a_negative_deposit_GB15()
    {
        var wallet = DriverWallet.Create(1);
        wallet.TopUpDeposit(50_000);

        var tx = wallet.DeductCommissionFromDeposit(10, 60_000);

        Assert.Equal(-10_000m, wallet.DepositBalance);
        Assert.Equal(-10_000m, tx.BalanceAfter);
    }

    [Fact]
    public void Negative_deposit_stops_the_driver_taking_cash_orders_GB16()
    {
        var wallet = DriverWallet.Create(1);
        wallet.TopUpDeposit(50_000);

        wallet.DeductCommissionFromDeposit(10, 60_000);

        Assert.False(wallet.CanReceiveCod(minDeposit: 0, orderCommission: 0));
    }

    [Fact]
    public void Withdrawal_more_than_earnings_is_rejected()
    {
        var wallet = DriverWallet.Create(1);
        wallet.CreditTripEarning(10, 100_000, 10_000);

        Assert.Throws<DomainException>(() => wallet.RequestWithdrawal(90_001));
    }

    [Fact]
    public void Rejected_withdrawal_gives_the_money_back_GB27()
    {
        var wallet = DriverWallet.Create(1);
        wallet.CreditTripEarning(10, 100_000, 10_000);
        var withdrawal = wallet.RequestWithdrawal(50_000);
        Assert.Equal(40_000m, wallet.EarningsBalance);

        wallet.RejectWithdrawal(withdrawal, adminId: 2, Now);

        Assert.Equal(90_000m, wallet.EarningsBalance);
        Assert.Equal(WalletTransactionStatus.Rejected, withdrawal.TransactionStatus);
    }

    [Fact]
    public void Completed_withdrawal_cannot_be_processed_twice()
    {
        var wallet = DriverWallet.Create(1);
        wallet.CreditTripEarning(10, 100_000, 10_000);
        var withdrawal = wallet.RequestWithdrawal(50_000);
        wallet.CompleteWithdrawal(withdrawal, 2, "FT123", Now);

        Assert.Throws<DomainException>(() => wallet.CompleteWithdrawal(withdrawal, 2, "FT124", Now));
        Assert.Equal("FT123", withdrawal.ReferenceCode);
    }

    [Fact]
    public void Withdrawal_of_another_wallet_cannot_be_processed()
    {
        var walletA = DriverWallet.Create(1);
        walletA.CreditTripEarning(10, 100_000, 10_000);
        var withdrawalOfA = walletA.RequestWithdrawal(50_000);

        var walletB = DriverWallet.Create(2);
        typeof(DriverWallet).BaseType!.BaseType!.BaseType!
            .GetProperty("Id")!.SetValue(walletB, 2L);

        Assert.Throws<DomainException>(() => walletB.RejectWithdrawal(withdrawalOfA, 2, Now));
        Assert.Equal(0m, walletB.EarningsBalance);
    }

    [Fact]
    public void Frozen_wallet_rejects_operations()
    {
        var wallet = DriverWallet.Create(1);
        typeof(DriverWallet).GetProperty("WalletStatus")!.SetValue(wallet, WalletStatus.Frozen);

        Assert.Throws<DomainException>(() => wallet.TopUpDeposit(1_000));
    }

    [Fact]
    public void Frozen_wallet_still_settles_a_finished_order_GB15()
    {
        var wallet = DriverWallet.Create(1);
        typeof(DriverWallet).GetProperty("WalletStatus")!.SetValue(wallet, WalletStatus.Frozen);

        wallet.CreditTripEarning(10, 100_000, 10_000);
        wallet.DeductCommissionFromDeposit(11, 5_000);

        Assert.Equal((90_000m, -5_000m), (wallet.EarningsBalance, wallet.DepositBalance));
    }

    [Fact]
    public void Top_up_below_50000_is_rejected_5_2()
    {
        var wallet = DriverWallet.Create(1);

        var error = Assert.Throws<DomainException>(() => wallet.TopUpDeposit(49_999));

        Assert.Equal("TOP_UP_TOO_SMALL", error.Code);
        wallet.TopUpDeposit(50_000);
        Assert.Equal(50_000m, wallet.DepositBalance);
    }

    [Fact]
    public void Withdrawal_has_no_minimum_5_1()
    {
        var wallet = DriverWallet.Create(1);
        wallet.CreditTripEarning(10, 100_000, 10_000);

        wallet.RequestWithdrawal(1_000);

        Assert.Equal(89_000m, wallet.EarningsBalance);
    }

    /// <summary>Earnings 90,000 and a deposit of -10,000.</summary>
    private static DriverWallet WalletOwingCommission()
    {
        var wallet = DriverWallet.Create(1);
        wallet.CreditTripEarning(10, 100_000, 10_000);
        wallet.TopUpDeposit(50_000);
        wallet.DeductCommissionFromDeposit(11, 60_000);
        return wallet;
    }

    [Fact]
    public void Withdrawal_pays_the_deposit_debt_from_earnings_first_5_1()
    {
        var wallet = WalletOwingCommission();

        var withdrawal = wallet.RequestWithdrawal(50_000);

        Assert.Equal((30_000m, 0m), (wallet.EarningsBalance, wallet.DepositBalance));
        Assert.Equal((50_000m, 30_000m), (withdrawal.Amount, withdrawal.BalanceAfter));
        var paid = Assert.Single(wallet.Transactions, t => t.TransactionType == WalletTransactionType.CommissionDebtPaid);
        Assert.Equal((10_000m, 0m, WalletTransactionStatus.Completed), (paid.Amount, paid.BalanceAfter, paid.TransactionStatus));
    }

    [Fact]
    public void Withdrawal_can_take_all_earnings_left_after_the_debt_5_1()
    {
        var wallet = WalletOwingCommission();

        wallet.RequestWithdrawal(80_000);

        Assert.Equal((0m, 0m), (wallet.EarningsBalance, wallet.DepositBalance));
    }

    [Fact]
    public void Withdrawal_above_what_is_left_after_the_debt_changes_nothing_5_1()
    {
        var wallet = WalletOwingCommission();
        var before = wallet.Transactions.Count;

        Assert.Throws<DomainException>(() => wallet.RequestWithdrawal(80_001));

        Assert.Equal((90_000m, -10_000m), (wallet.EarningsBalance, wallet.DepositBalance));
        Assert.Equal(before, wallet.Transactions.Count);
    }

    [Fact]
    public void Rejected_withdrawal_gives_back_the_amount_but_the_debt_stays_paid_5_1()
    {
        var wallet = WalletOwingCommission();
        var withdrawal = wallet.RequestWithdrawal(50_000);

        wallet.RejectWithdrawal(withdrawal, adminId: 2, Now);

        Assert.Equal((80_000m, 0m), (wallet.EarningsBalance, wallet.DepositBalance));
    }

    [Fact]
    public void Withdrawal_leaves_a_positive_deposit_alone_5_1()
    {
        var wallet = DriverWallet.Create(1);
        wallet.CreditTripEarning(10, 100_000, 10_000);
        wallet.TopUpDeposit(50_000);

        wallet.RequestWithdrawal(90_000);

        Assert.Equal((0m, 50_000m), (wallet.EarningsBalance, wallet.DepositBalance));
        Assert.DoesNotContain(wallet.Transactions, t => t.TransactionType == WalletTransactionType.CommissionDebtPaid);
    }

    [Fact]
    public void Payment_transaction_mark_paid_is_idempotent()
    {
        var tx = PaymentTransaction.ForOrderViaPayOs(10, 180_000, 123456, "https://pay.payos.vn/web/1", Now);

        Assert.True(tx.MarkPaid(Now.AddMinutes(1)));
        Assert.Equal(PaymentTransactionStatus.Paid, tx.TransactionStatus);
        Assert.Equal(Now.AddMinutes(1), tx.PaidAt);

        Assert.False(tx.MarkPaid(Now.AddMinutes(5)));
        Assert.Equal(Now.AddMinutes(1), tx.PaidAt);
    }

    [Fact]
    public void Payment_transaction_expires_after_10_minutes_only_while_pending_GB13()
    {
        var tx = PaymentTransaction.ForDeposit(1, 100_000, 123456, "https://pay.payos.vn/web/1", Now);

        Assert.False(tx.ExpireIfDue(Now.AddMinutes(9).AddSeconds(59)));
        Assert.True(tx.ExpireIfDue(Now.AddMinutes(10)));
        Assert.Equal(PaymentTransactionStatus.Expired, tx.TransactionStatus);
        Assert.False(tx.ExpireIfDue(Now.AddMinutes(11))); // already Expired

        // If the bank transfer actually succeeded at PayOS just before expiry, the late webhook still records Paid.
        Assert.True(tx.MarkPaid(Now.AddMinutes(11)));
        Assert.Equal(PaymentTransactionStatus.Paid, tx.TransactionStatus);
        Assert.False(tx.MarkPaid(Now.AddMinutes(12))); // idempotent once Paid
    }

    [Fact]
    public void Payment_transaction_locally_cancelled_still_records_paid_if_customer_paid_old_qr()
    {
        var tx = PaymentTransaction.ForOrderViaPayOs(10, 180_000, 123456, "https://pay.payos.vn/web/1", Now);
        tx.Cancel();
        Assert.Equal(PaymentTransactionStatus.Cancelled, tx.TransactionStatus);

        Assert.True(tx.MarkPaid(Now.AddMinutes(2)));
        Assert.Equal(PaymentTransactionStatus.Paid, tx.TransactionStatus);
        Assert.Equal(Now.AddMinutes(2), tx.PaidAt);
    }

    [Fact]
    public void Only_one_pending_withdrawal_is_allowed_at_a_time_GB32()
    {
        var wallet = DriverWallet.Create(1);
        wallet.CreditTripEarning(10, 200_000, 20_000); // 180,000 earnings

        var first = wallet.RequestWithdrawal(50_000);

        var secondWhilePending = Assert.Throws<DomainException>(() => wallet.RequestWithdrawal(30_000));
        Assert.Equal("WITHDRAWAL_ALREADY_PENDING", secondWhilePending.Code);
        Assert.Equal(130_000m, wallet.EarningsBalance);

        wallet.CompleteWithdrawal(first, adminId: 2, "FT001", Now);
        var afterProcessed = wallet.RequestWithdrawal(30_000);
        Assert.Equal(WalletTransactionStatus.Pending, afterProcessed.TransactionStatus);
        Assert.Equal(100_000m, wallet.EarningsBalance);
    }

    [Fact]
    public void Max_withdrawable_amount_subtracts_negative_deposit_debt_and_is_zero_when_frozen_5_1()
    {
        var wallet = WalletOwingCommission(); // earnings 90,000, deposit -10,000
        Assert.Equal(80_000m, wallet.MaxWithdrawableAmount);

        typeof(DriverWallet).GetProperty("WalletStatus")!.SetValue(wallet, WalletStatus.Frozen);
        Assert.Equal(0m, wallet.MaxWithdrawableAmount);
    }
}
