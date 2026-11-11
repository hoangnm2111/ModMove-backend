using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.UnitTests.Domain;

public class DriverWalletTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Orders_need_a_deposit_of_at_least_required_deposit_200000_GB16()
    {
        const decimal requiredDeposit = 200_000; // seeded REQUIRED_DEPOSIT
        const decimal commission = 18_000; // 10% of a 180,000 order
        var wallet = DriverWallet.Create(1);
        Assert.False(wallet.CanReceiveOrders(requiredDeposit));
        Assert.False(wallet.CanReceiveCod(requiredDeposit, commission));

        wallet.CreditTopUp(199_999);
        Assert.False(wallet.CanReceiveOrders(requiredDeposit));
        Assert.False(wallet.CanReceiveCod(requiredDeposit, commission));

        wallet.TopUpDeposit(1, requiredDeposit);
        Assert.True(wallet.CanReceiveOrders(requiredDeposit));
        Assert.True(wallet.CanReceiveCod(requiredDeposit, commission));
    }

    [Fact]
    public void Cod_also_needs_a_deposit_that_covers_the_order_commission_GB16()
    {
        var wallet = DriverWallet.Create(1);
        wallet.TopUpDeposit(200_000);

        Assert.True(wallet.CanReceiveCod(requiredDeposit: 200_000, orderCommission: 200_000));
        Assert.False(wallet.CanReceiveCod(requiredDeposit: 200_000, orderCommission: 250_000)); // 10% of a 2,500,000 order
    }

    [Fact]
    public void Trip_earning_is_fare_minus_commission_GB14()
    {
        var wallet = DriverWallet.Create(1);

        var tx = wallet.CreditTripEarning(orderId: 10, fare: 200_000, commission: 20_000);

        Assert.Equal(180_000m, wallet.EarningsBalance);
        Assert.Equal(180_000m, tx.BalanceAfter);
        Assert.Equal((180_000m, 0m), (tx.EarningsAmount, tx.DepositAmount));
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
        wallet.TopUpDeposit(200_000);

        var tx = wallet.DeductCommissionFromDeposit(10, 210_000);

        Assert.Equal(-10_000m, wallet.DepositBalance);
        Assert.Equal(-10_000m, tx.BalanceAfter);
    }

    [Fact]
    public void Negative_deposit_stops_the_driver_taking_orders_GB16()
    {
        var wallet = DriverWallet.Create(1);
        wallet.TopUpDeposit(200_000);

        wallet.DeductCommissionFromDeposit(10, 210_000);

        Assert.False(wallet.CanReceiveOrders(requiredDeposit: 200_000));
        Assert.False(wallet.CanReceiveCod(requiredDeposit: 0, orderCommission: 0));
    }

    [Fact]
    public void Withdrawal_more_than_wallet_total_is_rejected()
    {
        var wallet = DriverWallet.Create(1);
        wallet.CreditTripEarning(10, 100_000, 10_000);

        Assert.Throws<DomainException>(() => wallet.RequestWithdrawal(90_001));
    }

    [Fact]
    public void Rejected_withdrawal_gives_the_money_back_to_each_source_balance_GB27_GB32()
    {
        var wallet = DriverWallet.Create(1);
        wallet.TopUpDeposit(200_000);
        wallet.CreditTripEarning(10, 100_000, 10_000); // 90,000 earnings
        var withdrawal = wallet.RequestWithdrawal(140_000); // 90,000 from earnings + 50,000 from deposit
        Assert.Equal((0m, 150_000m), (wallet.EarningsBalance, wallet.DepositBalance));
        Assert.Equal((90_000m, 50_000m), (withdrawal.EarningsAmount, withdrawal.DepositAmount));

        wallet.RejectWithdrawal(withdrawal, adminId: 2, Now, requiredDeposit: 200_000);

        Assert.Equal((90_000m, 200_000m), (wallet.EarningsBalance, wallet.DepositBalance));
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

        Assert.Throws<DomainException>(() => wallet.TopUpDeposit(200_000));
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
    public void Top_up_below_missing_deposit_is_rejected_and_any_positive_amount_is_allowed_above_required_deposit_GB32()
    {
        var wallet = DriverWallet.Create(1);

        var error = Assert.Throws<DomainException>(() => wallet.TopUpDeposit(199_999, requiredDeposit: 200_000));
        Assert.Equal("TOP_UP_BELOW_MISSING_DEPOSIT", error.Code);

        wallet.TopUpDeposit(200_000, requiredDeposit: 200_000);
        Assert.Equal(200_000m, wallet.DepositBalance);

        // Once DepositBalance >= REQUIRED_DEPOSIT, any positive top-up is allowed (no 50,000 minimum)
        wallet.TopUpDeposit(1_000, requiredDeposit: 200_000);
        Assert.Equal(201_000m, wallet.DepositBalance);
    }

    [Fact]
    public void Withdrawal_has_no_minimum_GB32()
    {
        var wallet = DriverWallet.Create(1);
        wallet.CreditTripEarning(10, 100_000, 10_000);

        wallet.RequestWithdrawal(1_000);

        Assert.Equal(89_000m, wallet.EarningsBalance);
    }

    [Fact]
    public void RefillDepositFromEarnings_moves_earnings_to_deposit_up_to_required_deposit_GB32()
    {
        var wallet = DriverWallet.Create(1);
        wallet.TopUpDeposit(200_000);
        wallet.DeductCommissionFromDeposit(11, 60_000); // Deposit = 140,000 (short by 60,000)
        wallet.CreditTripEarning(10, 100_000, 10_000);  // Earnings = 90,000

        var refill = wallet.RefillDepositFromEarnings(requiredDeposit: 200_000);

        Assert.NotNull(refill);
        Assert.Equal((30_000m, 200_000m), (wallet.EarningsBalance, wallet.DepositBalance));
        Assert.Equal(
            (WalletTransactionType.DepositRefill, 60_000m, 60_000m, 60_000m, 200_000m, WalletTransactionStatus.Completed),
            (refill.TransactionType, refill.Amount, refill.EarningsAmount, refill.DepositAmount, refill.BalanceAfter, refill.TransactionStatus));
    }

    [Fact]
    public void Withdrawal_deducts_from_earnings_first_then_deposit_when_no_active_order_GB32()
    {
        var wallet = DriverWallet.Create(1);
        wallet.TopUpDeposit(200_000);
        wallet.CreditTripEarning(10, 100_000, 10_000); // Earnings = 90,000, Deposit = 200,000, Total = 290,000

        var withdrawal = wallet.RequestWithdrawal(240_000, hasActiveOrder: false);

        Assert.Equal((0m, 50_000m, 50_000m), (wallet.EarningsBalance, wallet.DepositBalance, wallet.WalletTotal));
        Assert.Equal((240_000m, 90_000m, 150_000m, 50_000m),
            (withdrawal.Amount, withdrawal.EarningsAmount, withdrawal.DepositAmount, withdrawal.BalanceAfter));
    }

    [Fact]
    public void Withdrawal_while_driver_has_active_order_cannot_touch_deposit_balance_GB32()
    {
        var wallet = DriverWallet.Create(1);
        wallet.TopUpDeposit(200_000);
        wallet.CreditTripEarning(10, 100_000, 10_000); // Earnings = 90,000, Deposit = 200,000

        var ex = Assert.Throws<DomainException>(() => wallet.RequestWithdrawal(90_001, hasActiveOrder: true));
        Assert.Equal("WITHDRAWAL_EXCEEDS_BALANCE", ex.Code);
        Assert.Equal((90_000m, 200_000m), (wallet.EarningsBalance, wallet.DepositBalance));

        var withdrawal = wallet.RequestWithdrawal(90_000, hasActiveOrder: true);
        Assert.Equal((0m, 200_000m), (wallet.EarningsBalance, wallet.DepositBalance));
        Assert.Equal((90_000m, 0m), (withdrawal.EarningsAmount, withdrawal.DepositAmount));
    }

    [Fact]
    public void Rejected_withdrawal_triggers_deposit_refill_when_deposit_was_below_required_GB32()
    {
        var wallet = DriverWallet.Create(1);
        wallet.CreditTopUp(150_000);
        wallet.CreditTripEarning(10, 100_000, 10_000); // Earnings = 90,000, Deposit = 150,000
        var withdrawal = wallet.RequestWithdrawal(90_000, hasActiveOrder: false); // takes 90k from earnings
        Assert.Equal((0m, 150_000m), (wallet.EarningsBalance, wallet.DepositBalance));

        wallet.RejectWithdrawal(withdrawal, adminId: 2, Now, requiredDeposit: 200_000);

        // 90,000 returned to earnings, then 50,000 auto-refilled into deposit so Deposit reaches 200,000!
        Assert.Equal((40_000m, 200_000m), (wallet.EarningsBalance, wallet.DepositBalance));
        Assert.Contains(wallet.Transactions, t => t.TransactionType == WalletTransactionType.DepositRefill && t.Amount == 50_000m);
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
    public void GetMaxWithdrawable_returns_wallet_total_or_earnings_when_active_order_and_zero_when_frozen_GB32()
    {
        var wallet = DriverWallet.Create(1);
        wallet.TopUpDeposit(200_000);
        wallet.CreditTripEarning(10, 100_000, 10_000); // earnings 90,000, deposit 200,000, total 290,000
        Assert.Equal(290_000m, wallet.GetMaxWithdrawable(hasActiveOrder: false));
        Assert.Equal(90_000m, wallet.GetMaxWithdrawable(hasActiveOrder: true));

        typeof(DriverWallet).GetProperty("WalletStatus")!.SetValue(wallet, WalletStatus.Frozen);
        Assert.Equal(0m, wallet.GetMaxWithdrawable(hasActiveOrder: false));
    }
}
