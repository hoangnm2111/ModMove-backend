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
}
