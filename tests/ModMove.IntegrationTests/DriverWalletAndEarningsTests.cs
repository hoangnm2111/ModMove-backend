using System.Data;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Wallets.Commands.RequestWithdrawal;
using ModMove.Application.Features.Wallets.Commands.UpdateDriverBankAccount;
using ModMove.Application.Features.Wallets.Queries.GetDriverEarnings;
using ModMove.Application.Features.Wallets.Queries.GetDriverWallet;
using ModMove.Application.Features.Wallets.Queries.GetDriverWalletTransactions;
using ModMove.Application.Features.Wallets.Queries.GetDriverWithdrawals;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

/// <summary>
/// Driver wallet, bank account, withdrawals and earnings breakdown: UC-DRV-20, 22, 23; DECISIONS 5.1, M1, 9.5; GB-16, GB-32.
/// </summary>
public class DriverWalletAndEarningsTests
{
    private sealed record Setup(TestApp App, User Driver, DriverProfile Profile, DriverWallet Wallet);

    private static Setup Build(bool approved = true, bool linkBank = false)
    {
        var driver = User.Create("Nguyễn Văn Tài", "0912000020", "drv20@x.vn", "h:x", UserRole.Driver, emailVerified: true);
        var app = TestApp.SignedInAs(driver);

        var profile = DriverProfile.Create(driver.Id, "079000000020", "79000020", "f.jpg", "b.jpg");
        app.DriverProfiles.Add(profile);
        if (approved)
            profile.Approve(Vehicle.Create(profile.Id, 1, "51C-202.02", 1000, "200x150x150", "f.jpg", "f.jpg", "f.jpg"));
        if (linkBank)
            profile.LinkBankAccount("Vietcombank", "0071009999999", "NGUYEN VAN TAI");

        var wallet = DriverWallet.Create(profile.Id);
        app.DriverWallets.Add(wallet);
        app.Settings.Add(PlatformSetting.Create(SettingKeys.RequiredDeposit, "200000", TestApp.Start));

        return new Setup(app, driver, profile, wallet);
    }

    [Fact]
    public async Task Driver_links_bank_account_and_requests_withdrawal_from_earnings_first_then_deposit_DRV22_GB32()
    {
        var s = Build(approved: true, linkBank: false);
        s.Wallet.CreditTripEarning(orderId: 10, fare: 200_000m, commission: 20_000m); // +180,000 earnings
        s.Wallet.TopUpDeposit(200_000m); // +200,000 deposit (WalletTotal = 380,000)

        // 1. Without a bank account -> 422 BANK_ACCOUNT_REQUIRED
        var noBank = await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new RequestWithdrawalCommand(100_000m)));
        Assert.Equal("BANK_ACCOUNT_REQUIRED", noBank.Code);

        // 2. Link bank account via dedicated endpoint
        await s.App.Sender.Send(new UpdateDriverBankAccountCommand("  Vietcombank ", " 0071008888888 ", " NGUYEN VAN TAI "));
        Assert.Equal(("Vietcombank", "0071008888888", "NGUYEN VAN TAI"),
            (s.Profile.BankName, s.Profile.BankAccountNumber, s.Profile.BankAccountHolder));

        // 3. Request 250,000 withdrawal -> 180,000 from earnings first + 70,000 from deposit
        var res = await s.App.Sender.Send(new RequestWithdrawalCommand(250_000m));

        Assert.True(res.TransactionId > 0);
        Assert.Equal((250_000m, 180_000m, 70_000m, 0m, 130_000m, 130_000m, true, "Pending"),
            (res.Amount, res.EarningsDeducted, res.DepositDeducted, res.EarningsBalanceAfter,
             res.DepositBalanceAfter, res.WalletTotalAfter, res.DepositBelowRequired, res.TransactionStatus));
        Assert.Contains(s.Wallet.Transactions, t =>
            t.TransactionType == WalletTransactionType.Withdrawal
            && t.Amount == 250_000m
            && t.EarningsAmount == 180_000m
            && t.DepositAmount == 70_000m);

        // 4. Second request while one is still Pending -> 422 WITHDRAWAL_ALREADY_PENDING (GB-32)
        var duplicatePending = await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new RequestWithdrawalCommand(10_000m)));
        Assert.Equal("WITHDRAWAL_ALREADY_PENDING", duplicatePending.Code);
    }

    [Fact]
    public async Task RequestWithdrawal_accepts_inline_bank_account_with_no_minimum_amount_5_1()
    {
        var s = Build(approved: true, linkBank: false);
        s.Wallet.CreditTripEarning(orderId: 1, fare: 50_000m, commission: 5_000m); // 45,000 earnings

        var res = await s.App.Sender.Send(new RequestWithdrawalCommand(
            Amount: 1_000m,
            BankName: "ACB",
            BankAccountNumber: "123456789",
            BankAccountHolder: "NGUYEN VAN TAI"));

        Assert.Equal((1_000m, 44_000m), (res.Amount, res.EarningsBalanceAfter));
        Assert.Equal(("ACB", "123456789"), (s.Profile.BankName, s.Profile.BankAccountNumber));
    }

    [Fact]
    public async Task Driver_queries_wallet_overview_transactions_withdrawals_and_vietnam_time_earnings_DRV20_22_23()
    {
        var s = Build(approved: true, linkBank: true);

        // 1. GetDriverWalletQuery (earnings 300k, deposit -25k -> walletTotal 275k, missingDeposit 225k, maxWithdrawable 275k, canReceiveOrders false)
        s.App.Sql.Results.Enqueue(ContextTable(s.Profile.Id));
        var walletTable = new DataTable();
        walletTable.Columns.Add("WalletId", typeof(long));
        walletTable.Columns.Add("WalletStatus", typeof(string));
        walletTable.Columns.Add("DepositBalance", typeof(decimal));
        walletTable.Columns.Add("EarningsBalance", typeof(decimal));
        walletTable.Columns.Add("PendingWithdrawalAmount", typeof(decimal));
        walletTable.Columns.Add("ActiveOrderId", typeof(long));
        walletTable.Rows.Add(1L, "Active", -25_000m, 300_000m, 50_000m, DBNull.Value);
        s.App.Sql.Results.Enqueue(walletTable);

        var overview = await s.App.Sender.Send(new GetDriverWalletQuery());
        Assert.Equal((275_000m, 225_000m, 275_000m, 200_000m, false, false, true, 50_000m),
            (overview.WalletTotal, overview.MissingDepositAmount, overview.MaxWithdrawableAmount, overview.RequiredDeposit,
             overview.CanReceiveOrders, overview.HasActiveOrder, overview.HasPendingWithdrawal, overview.PendingWithdrawalAmount));
        Assert.Equal("0071009999999", overview.BankAccount!.BankAccountNumber);

        // 2. GetDriverWalletTransactionsQuery
        s.App.Sql.Results.Enqueue(ContextTable(s.Profile.Id));
        s.App.Sql.ScalarResult = 1;
        var txTable = new DataTable();
        txTable.Columns.Add("Id", typeof(long));
        txTable.Columns.Add("TransactionType", typeof(string));
        txTable.Columns.Add("Amount", typeof(decimal));
        txTable.Columns.Add("EarningsAmount", typeof(decimal));
        txTable.Columns.Add("DepositAmount", typeof(decimal));
        txTable.Columns.Add("BalanceAfter", typeof(decimal));
        txTable.Columns.Add("TransactionStatus", typeof(string));
        txTable.Columns.Add("OrderId", typeof(long));
        txTable.Columns.Add("OrderCode", typeof(string));
        txTable.Columns.Add("ReferenceCode", typeof(string));
        txTable.Columns.Add("ProcessedAt", typeof(DateTime));
        txTable.Columns.Add("CreatedAt", typeof(DateTime));
        txTable.Rows.Add(101L, "TripEarning", 180_000m, 180_000m, 0m, 300_000m, "Completed", 9L, "MM20261108009", null, DBNull.Value, TestApp.Start.UtcDateTime);
        s.App.Sql.Results.Enqueue(txTable);

        var txs = await s.App.Sender.Send(new GetDriverWalletTransactionsQuery(Type: WalletTransactionType.TripEarning));
        var txItem = Assert.Single(txs.Items);
        Assert.Equal(("TripEarning", "MM20261108009", 180_000m, 180_000m, 0m),
            (txItem.TransactionType, txItem.OrderCode, txItem.Amount, txItem.EarningsAmount, txItem.DepositAmount));

        // 3. GetDriverWithdrawalsQuery
        s.App.Sql.Results.Enqueue(ContextTable(s.Profile.Id));
        s.App.Sql.ScalarResult = 1;
        var wdTable = new DataTable();
        wdTable.Columns.Add("TransactionId", typeof(long));
        wdTable.Columns.Add("Amount", typeof(decimal));
        wdTable.Columns.Add("EarningsAmount", typeof(decimal));
        wdTable.Columns.Add("DepositAmount", typeof(decimal));
        wdTable.Columns.Add("BalanceAfter", typeof(decimal));
        wdTable.Columns.Add("TransactionStatus", typeof(string));
        wdTable.Columns.Add("ReferenceCode", typeof(string));
        wdTable.Columns.Add("ProcessedAt", typeof(DateTime));
        wdTable.Columns.Add("CreatedAt", typeof(DateTime));
        wdTable.Rows.Add(202L, 50_000m, 30_000m, 20_000m, 250_000m, "Completed", "FT999", TestApp.Start.UtcDateTime, TestApp.Start.UtcDateTime);
        s.App.Sql.Results.Enqueue(wdTable);

        var wds = await s.App.Sender.Send(new GetDriverWithdrawalsQuery(Status: WalletTransactionStatus.Completed));
        var wdItem = Assert.Single(wds.Items);
        Assert.Equal(("FT999", "Vietcombank", 30_000m, 20_000m),
            (wdItem.ReferenceCode, wdItem.BankName, wdItem.EarningsAmount, wdItem.DepositAmount));

        // 4. GetDriverEarningsQuery (Today in Vietnam UTC+7)
        s.App.Sql.Results.Enqueue(ContextTable(s.Profile.Id));
        var summaryTable = new DataTable();
        summaryTable.Columns.Add("CompletedTrips", typeof(int));
        summaryTable.Columns.Add("GrossFares", typeof(decimal));
        summaryTable.Columns.Add("TotalCommission", typeof(decimal));
        summaryTable.Columns.Add("NetEarnings", typeof(decimal));
        summaryTable.Rows.Add(1, 180_000m, 18_000m, 162_000m);
        s.App.Sql.Results.Enqueue(summaryTable);

        var itemsTable = new DataTable();
        itemsTable.Columns.Add("OrderId", typeof(long));
        itemsTable.Columns.Add("OrderCode", typeof(string));
        itemsTable.Columns.Add("PickupAddress", typeof(string));
        itemsTable.Columns.Add("DropoffAddress", typeof(string));
        itemsTable.Columns.Add("PaymentMethod", typeof(string));
        itemsTable.Columns.Add("DistanceFee", typeof(decimal));
        itemsTable.Columns.Add("ServiceFee", typeof(decimal));
        itemsTable.Columns.Add("DiscountAmount", typeof(decimal));
        itemsTable.Columns.Add("TotalFee", typeof(decimal));
        itemsTable.Columns.Add("CommissionRate", typeof(decimal));
        itemsTable.Columns.Add("CommissionAmount", typeof(decimal));
        itemsTable.Columns.Add("DriverEarning", typeof(decimal));
        itemsTable.Columns.Add("CompletedAt", typeof(DateTime));
        itemsTable.Rows.Add(9L, "MM20261108009", "A", "B", "PayOS", 200_000m, 0m, 20_000m, 180_000m, 0.10m, 18_000m, 162_000m, TestApp.Start.UtcDateTime);
        s.App.Sql.Results.Enqueue(itemsTable);

        var earnings = await s.App.Sender.Send(new GetDriverEarningsQuery(Period: EarningsPeriod.Today));
        Assert.Equal((1, 180_000m, 18_000m, 162_000m),
            (earnings.CompletedTrips, earnings.GrossFares, earnings.TotalCommission, earnings.NetEarnings));
        Assert.Equal(162_000m, Assert.Single(earnings.Orders.Items).DriverEarning);
        Assert.NotNull(s.App.Sql.LastParameters["FromUtc"]);
        Assert.NotNull(s.App.Sql.LastParameters["ToUtc"]);
    }

    private static DataTable ContextTable(long driverProfileId)
    {
        var dt = new DataTable();
        dt.Columns.Add("AccountStatus", typeof(string));
        dt.Columns.Add("DriverProfileId", typeof(long));
        dt.Columns.Add("ApprovalStatus", typeof(string));
        dt.Columns.Add("BankName", typeof(string));
        dt.Columns.Add("BankAccountNumber", typeof(string));
        dt.Columns.Add("BankAccountHolder", typeof(string));
        dt.Rows.Add("Active", driverProfileId, "Approved", "Vietcombank", "0071009999999", "NGUYEN VAN TAI");
        return dt;
    }
}

