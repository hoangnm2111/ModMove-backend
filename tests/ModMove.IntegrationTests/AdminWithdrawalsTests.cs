using System.Data;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Wallets.Commands.ApproveWithdrawal;
using ModMove.Application.Features.Wallets.Commands.RejectWithdrawal;
using ModMove.Application.Features.Wallets.Queries.GetAdminWithdrawalDetail;
using ModMove.Application.Features.Wallets.Queries.GetAdminWithdrawals;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

/// <summary>
/// Screen #39: UC-ADM-07, GB-27, DECISIONS 5.1, TEAM-PLAN lines 89, 94.
/// </summary>
public class AdminWithdrawalsTests
{
    private sealed record Setup(TestApp App, User Admin, User DriverUser, DriverProfile Profile, DriverWallet Wallet);

    private static Setup Build(AccountStatus adminStatus = AccountStatus.Active)
    {
        var admin = User.Create("Quản trị viên", "0900000001", "admin@modmove.vn", "h:x", UserRole.Administrator, emailVerified: true);
        admin.ChangeStatus(adminStatus);
        var driverUser = User.Create("Nguyễn Văn Tài", "0912345678", "driver@modmove.vn", "h:x", UserRole.Driver, emailVerified: true);
        driverUser.SetFcmToken("fcm-driver-token-999");

        var app = TestApp.SignedInAs(admin, driverUser);

        var profile = DriverProfile.Create(driverUser.Id, "front.jpg", "back.jpg");
        app.DriverProfiles.Add(profile);

        var wallet = DriverWallet.Create(profile.Id);
        app.DriverWallets.Add(wallet);
        app.Settings.Add(PlatformSetting.Create(SettingKeys.RequiredDeposit, "200000", TestApp.Start));

        return new Setup(app, admin, driverUser, profile, wallet);
    }

    // ==========================================
    // 1. Queries (Task 3.1)
    // ==========================================

    [Fact]
    public async Task Admin_can_list_withdrawals_with_filters()
    {
        var s = Build();

        var dt = new DataTable();
        dt.Columns.Add("TransactionId", typeof(long));
        dt.Columns.Add("WalletId", typeof(long));
        dt.Columns.Add("DriverProfileId", typeof(long));
        dt.Columns.Add("DriverUserId", typeof(long));
        dt.Columns.Add("DriverName", typeof(string));
        dt.Columns.Add("DriverPhone", typeof(string));
        dt.Columns.Add("Amount", typeof(decimal));
        dt.Columns.Add("EarningsAmount", typeof(decimal));
        dt.Columns.Add("DepositAmount", typeof(decimal));
        dt.Columns.Add("BalanceAfter", typeof(decimal));
        dt.Columns.Add("TransactionStatus", typeof(string));
        dt.Columns.Add("BankName", typeof(string));
        dt.Columns.Add("BankAccountNumber", typeof(string));
        dt.Columns.Add("BankAccountHolder", typeof(string));
        dt.Columns.Add("ReferenceCode", typeof(string));
        dt.Columns.Add("ProcessedBy", typeof(long));
        dt.Columns.Add("ProcessedAt", typeof(DateTime));
        dt.Columns.Add("CreatedAt", typeof(DateTime));

        dt.Rows.Add(
            20L, 1L, 1L, s.DriverUser.Id, "Nguyễn Văn Tài", "0912345678",
            500_000m, 400_000m, 100_000m, 100_000m, "Pending", "Vietcombank", "0071001234567", "NGUYEN VAN TAI",
            null, DBNull.Value, DBNull.Value, TestApp.Start.UtcDateTime);

        s.App.Sql.ScalarResult = 1;
        s.App.Sql.Result = dt;

        var page = await s.App.Sender.Send(new GetAdminWithdrawalsQuery(Page: 1, PageSize: 20, Status: "Pending", Search: "0912345678"));

        Assert.Equal(1, page.TotalCount);
        var item = Assert.Single(page.Items);
        Assert.Equal(20L, item.TransactionId);
        Assert.Equal((500_000m, 400_000m, 100_000m), (item.Amount, item.EarningsAmount, item.DepositAmount));
        Assert.Equal("Vietcombank", item.BankName);
        Assert.Contains("wt.transaction_type = 'Withdrawal'", s.App.Sql.LastSql);
        Assert.Contains("wt.transaction_status = @Status", s.App.Sql.LastSql);
    }

    [Fact]
    public async Task Admin_can_view_withdrawal_detail_and_not_found_throws()
    {
        var s = Build();

        var dt = new DataTable();
        dt.Columns.Add("TransactionId", typeof(long));
        dt.Columns.Add("WalletId", typeof(long));
        dt.Columns.Add("DriverProfileId", typeof(long));
        dt.Columns.Add("DriverUserId", typeof(long));
        dt.Columns.Add("DriverName", typeof(string));
        dt.Columns.Add("DriverPhone", typeof(string));
        dt.Columns.Add("DriverEmail", typeof(string));
        dt.Columns.Add("Amount", typeof(decimal));
        dt.Columns.Add("EarningsAmount", typeof(decimal));
        dt.Columns.Add("DepositAmount", typeof(decimal));
        dt.Columns.Add("BalanceAfter", typeof(decimal));
        dt.Columns.Add("CurrentEarningsBalance", typeof(decimal));
        dt.Columns.Add("CurrentDepositBalance", typeof(decimal));
        dt.Columns.Add("WalletStatus", typeof(string));
        dt.Columns.Add("TransactionStatus", typeof(string));
        dt.Columns.Add("BankName", typeof(string));
        dt.Columns.Add("BankAccountNumber", typeof(string));
        dt.Columns.Add("BankAccountHolder", typeof(string));
        dt.Columns.Add("ReferenceCode", typeof(string));
        dt.Columns.Add("ProcessedBy", typeof(long));
        dt.Columns.Add("ProcessedByName", typeof(string));
        dt.Columns.Add("ProcessedAt", typeof(DateTime));
        dt.Columns.Add("CreatedAt", typeof(DateTime));

        dt.Rows.Add(
            20L, 1L, 1L, s.DriverUser.Id, "Nguyễn Văn Tài", "0912345678", "driver@modmove.vn",
            500_000m, 400_000m, 100_000m, 100_000m, 100_000m, 250_000m, "Active", "Pending",
            "Vietcombank", "0071001234567", "NGUYEN VAN TAI",
            null, DBNull.Value, null, DBNull.Value, TestApp.Start.UtcDateTime);

        s.App.Sql.Result = dt;

        var detail = await s.App.Sender.Send(new GetAdminWithdrawalDetailQuery(20));

        Assert.Equal(20L, detail.TransactionId);
        Assert.Equal((500_000m, 400_000m, 100_000m), (detail.Amount, detail.EarningsAmount, detail.DepositAmount));
        Assert.Equal("0071001234567", detail.BankAccountNumber);

        s.App.Sql.Result = new DataTable();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            s.App.Sender.Send(new GetAdminWithdrawalDetailQuery(999)));
    }

    // ==========================================
    // 2. Approve Withdrawal (Task 3.2)
    // ==========================================

    [Fact]
    public async Task Admin_can_approve_pending_withdrawal_with_bank_reference_code_and_notify_driver()
    {
        var s = Build();
        s.Wallet.CreditTripEarning(orderId: 10, fare: 500_000m, commission: 50_000m); // 450,000 earnings
        var withdrawal = s.Wallet.RequestWithdrawal(300_000m); // 150,000 left
        s.App.DriverWallets.Update(s.Wallet);

        await s.App.Sender.Send(new ApproveWithdrawalCommand(withdrawal.Id, "  FT26305999888  "));

        Assert.Equal(WalletTransactionStatus.Completed, withdrawal.TransactionStatus);
        Assert.Equal("FT26305999888", withdrawal.ReferenceCode);
        Assert.Equal(s.Admin.Id, withdrawal.ProcessedBy);
        Assert.Equal(TestApp.Start, withdrawal.ProcessedAt);
        Assert.Equal(150_000m, s.Wallet.EarningsBalance);
        Assert.Equal(1, s.App.UnitOfWork.Saves);

        var signalR = Assert.Single(s.App.Realtime.Sent, m => m.EventName == RealtimeEvents.NotificationReceived);
        Assert.Equal(("user", s.DriverUser.Id.ToString()), (signalR.Target, signalR.Id));
        var payload = Assert.IsType<Notification>(signalR.Payload);
        Assert.Equal("WITHDRAWAL_DECIDED", payload.Type);
        Assert.Single(s.App.Push.Sent);
        Assert.Equal("fcm-driver-token-999", s.App.Push.Sent[0].Token);
        Assert.Contains("FT26305999888", s.App.Push.Sent[0].Body);
    }

    [Fact]
    public async Task Admin_cannot_approve_already_completed_or_non_withdrawal_transaction()
    {
        var s = Build();
        var earningTx = s.Wallet.CreditTripEarning(orderId: 10, fare: 500_000m, commission: 50_000m);
        var withdrawal = s.Wallet.RequestWithdrawal(200_000m);
        s.App.DriverWallets.Update(s.Wallet);

        // Non-withdrawal transaction (TripEarning) cannot be approved as withdrawal
        await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new ApproveWithdrawalCommand(earningTx.Id, "FT111")));

        // Complete withdrawal once
        await s.App.Sender.Send(new ApproveWithdrawalCommand(withdrawal.Id, "FT222"));

        // Second approval attempt throws DomainException
        await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new ApproveWithdrawalCommand(withdrawal.Id, "FT333")));
    }

    // ==========================================
    // 3. Reject Withdrawal (Task 3.3)
    // ==========================================

    [Fact]
    public async Task Admin_can_reject_pending_withdrawal_and_restore_earnings_balance()
    {
        var s = Build();
        s.Wallet.TopUpDeposit(200_000m);
        s.Wallet.CreditTripEarning(orderId: 10, fare: 500_000m, commission: 50_000m); // 450,000
        var withdrawal = s.Wallet.RequestWithdrawal(300_000m); // 150,000 left in earnings, 200,000 in deposit
        s.App.DriverWallets.Update(s.Wallet);

        await s.App.Sender.Send(new RejectWithdrawalCommand(withdrawal.Id, "Số tài khoản ngân hàng thụ hưởng không khớp tên chủ thẻ."));

        Assert.Equal(WalletTransactionStatus.Rejected, withdrawal.TransactionStatus);
        Assert.Equal(s.Admin.Id, withdrawal.ProcessedBy);
        Assert.Equal(TestApp.Start, withdrawal.ProcessedAt);
        Assert.Equal((450_000m, 200_000m), (s.Wallet.EarningsBalance, s.Wallet.DepositBalance));
        Assert.Equal(1, s.App.UnitOfWork.Saves);

        var signalR = Assert.Single(s.App.Realtime.Sent, m => m.EventName == RealtimeEvents.NotificationReceived);
        Assert.Equal(("user", s.DriverUser.Id.ToString()), (signalR.Target, signalR.Id));
        var payload = Assert.IsType<Notification>(signalR.Payload);
        Assert.Equal("WITHDRAWAL_DECIDED", payload.Type);
        Assert.Single(s.App.Push.Sent);
        Assert.Contains("bị từ chối", s.App.Push.Sent[0].Body);
    }

    [Fact]
    public async Task Admin_rejecting_withdrawal_restores_each_held_part_to_its_source_balance_GB32()
    {
        var s = Build();
        s.Wallet.TopUpDeposit(200_000m);
        s.Wallet.CreditTripEarning(orderId: 10, fare: 100_000m, commission: 10_000m); // +90,000 earnings, +200,000 deposit

        // Requesting 150,000 withdrawal takes 90,000 from earnings + 60,000 from deposit -> Earnings = 0, Deposit = 140,000
        var withdrawal = s.Wallet.RequestWithdrawal(150_000m);
        s.App.DriverWallets.Update(s.Wallet);
        Assert.Equal((0m, 140_000m), (s.Wallet.EarningsBalance, s.Wallet.DepositBalance));

        await s.App.Sender.Send(new RejectWithdrawalCommand(withdrawal.Id, "Tạm hoãn đối soát."));

        // 90,000 is returned to earnings and 60,000 is returned to deposit
        Assert.Equal((90_000m, 200_000m), (s.Wallet.EarningsBalance, s.Wallet.DepositBalance));
        Assert.Equal(WalletTransactionStatus.Rejected, withdrawal.TransactionStatus);
    }

    [Fact]
    public async Task Admin_cannot_reject_already_processed_withdrawal()
    {
        var s = Build();
        s.Wallet.CreditTripEarning(orderId: 10, fare: 500_000m, commission: 50_000m);
        var withdrawal = s.Wallet.RequestWithdrawal(200_000m);
        s.App.DriverWallets.Update(s.Wallet);

        await s.App.Sender.Send(new RejectWithdrawalCommand(withdrawal.Id, "Từ chối lần 1."));

        await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new RejectWithdrawalCommand(withdrawal.Id, "Từ chối lần 2.")));
    }

    [Fact]
    public async Task Inactive_admin_cannot_approve_or_reject_withdrawal()
    {
        var s = Build(AccountStatus.Locked);
        s.Wallet.CreditTripEarning(orderId: 10, fare: 500_000m, commission: 50_000m);
        var withdrawal = s.Wallet.RequestWithdrawal(200_000m);
        s.App.DriverWallets.Update(s.Wallet);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            s.App.Sender.Send(new ApproveWithdrawalCommand(withdrawal.Id, "FT999")));

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            s.App.Sender.Send(new RejectWithdrawalCommand(withdrawal.Id, "Lý do")));
    }

    [Fact]
    public async Task Admin_approving_or_rejecting_non_existent_withdrawal_throws_NotFoundException()
    {
        var s = Build();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            s.App.Sender.Send(new ApproveWithdrawalCommand(999, "FT999")));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            s.App.Sender.Send(new RejectWithdrawalCommand(999, "Lý do")));
    }

    [Fact]
    public async Task Validation_rejects_invalid_withdrawal_commands_and_queries()
    {
        var s = Build();

        await Assert.ThrowsAsync<ValidationException>(() =>
            s.App.Sender.Send(new GetAdminWithdrawalsQuery(Page: 0, PageSize: 20)));

        await Assert.ThrowsAsync<ValidationException>(() =>
            s.App.Sender.Send(new GetAdminWithdrawalsQuery(Page: 1, PageSize: 20, Status: "InvalidStatus")));

        await Assert.ThrowsAsync<ValidationException>(() =>
            s.App.Sender.Send(new ApproveWithdrawalCommand(1, "   ")));

        await Assert.ThrowsAsync<ValidationException>(() =>
            s.App.Sender.Send(new ApproveWithdrawalCommand(1, new string('X', 51))));

        await Assert.ThrowsAsync<ValidationException>(() =>
            s.App.Sender.Send(new RejectWithdrawalCommand(0, "Lý do")));
    }

    [Fact]
    public async Task Admin_rejecting_withdrawal_with_WrongBankAccount_marks_Failed_restores_balance_and_notifies_driver()
    {
        var s = Build();
        s.Profile.LinkBankAccount("Vietcombank", "0071001234567", "NGUYEN VAN TAI");
        s.Wallet.TopUpDeposit(200_000m);
        s.Wallet.CreditTripEarning(orderId: 10, fare: 500_000m, commission: 50_000m); // 450,000
        var withdrawal = s.Wallet.RequestWithdrawal(300_000m);
        s.App.DriverWallets.Update(s.Wallet);

        await s.App.Sender.Send(new RejectWithdrawalCommand(
            withdrawal.Id,
            ReasonCode: PayoutRejectionReasonCode.WrongBankAccount,
            Note: "Số tài khoản không tồn tại"));

        Assert.Equal(WalletTransactionStatus.Failed, withdrawal.TransactionStatus);
        Assert.False(s.Wallet.HasPendingWithdrawal);
        Assert.Equal((450_000m, 200_000m), (s.Wallet.EarningsBalance, s.Wallet.DepositBalance));

        var signalR = Assert.Single(s.App.Realtime.Sent, m => m.EventName == RealtimeEvents.NotificationReceived);
        var payload = Assert.IsType<Notification>(signalR.Payload);
        Assert.Equal("WITHDRAWAL_DECIDED", payload.Type);
        Assert.Equal("WRONG_BANK_ACCOUNT", payload.Data!["reasonCode"]);
        Assert.Equal("Failed", payload.Data["status"]);
        Assert.Contains("cập nhật lại tài khoản ngân hàng", payload.Body);
    }

    [Fact]
    public async Task Admin_approving_withdrawal_with_malformed_bank_account_throws_WRONG_BANK_ACCOUNT()
    {
        var s = Build();
        s.Profile.LinkBankAccount("Vietcombank", "12", "NGUYEN VAN TAI"); // invalid short account number
        s.Wallet.CreditTripEarning(orderId: 10, fare: 500_000m, commission: 50_000m);
        var withdrawal = s.Wallet.RequestWithdrawal(100_000m);
        s.App.DriverWallets.Update(s.Wallet);

        var ex = await Assert.ThrowsAsync<WrongBankAccountException>(() =>
            s.App.Sender.Send(new ApproveWithdrawalCommand(withdrawal.Id, "FT12345")));

        Assert.Equal("WRONG_BANK_ACCOUNT", ex.Code);
        Assert.Equal(WalletTransactionStatus.Pending, withdrawal.TransactionStatus);
        Assert.Equal(0, s.App.UnitOfWork.Saves);
    }
}
