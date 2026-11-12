using ModMove.Application.Common.Exceptions;
using ModMove.Application.Features.Users.Commands.DeleteMyDriverAccount;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

/// <summary>
/// DECISIONS Q12, B8 (2026-11-12): DELETE api/driver/account. The account becomes Inactive like a customer's, once no order is
/// running, no withdrawal is waiting and the wallet holds nothing (in either direction).
/// </summary>
public class DriverAccountTests
{
    private sealed class Setup
    {
        public User Driver { get; } = User.Create("Tài", "0900000003", "tai@x.vn", "h", UserRole.Driver, emailVerified: true);
        public TestApp App { get; }
        public DriverProfile? Profile { get; }
        public DriverWallet? Wallet { get; }

        public Setup(bool withProfile = true)
        {
            App = TestApp.SignedInAs(Driver);
            Driver.SetFcmToken("fcm-driver");
            if (!withProfile)
                return;

            Profile = DriverProfile.Create(Driver.Id, "001", "B2-001", "front.jpg", "back.jpg");
            Profile.Approve(Vehicle.Create(1, 1, "51C-1", 1000, "250x160x160", "r", "i", "p"));
            Profile.GoOnline();
            App.DriverProfiles.Add(Profile);
            Wallet = DriverWallet.Create(Profile.Id);
            App.DriverWallets.Add(Wallet);
        }

        public ShippingOrder RunningOrder()
        {
            var order = ShippingOrder.Create(
                "MM20261020001", 77, 1, null, "A", 21.0m, 105.8m, "B", 21.1m, 105.9m, "Bình", "0912345678",
                12m, 30, false, 200_000, 0, 0, PaymentMethod.Cash, PayerRole.Sender,
                CargoDetail.Create("FURNITURE", 1, 50, 100, 50, 50, ["a.jpg"], null), TestApp.Start);
            order.Accept(Profile!.Id, Driver.Id, TestApp.Start);
            App.Orders.Add(order);
            return order;
        }
    }

    [Fact]
    public async Task A_driver_with_nothing_open_deletes_the_account_B8()
    {
        var s = new Setup();
        var token = RefreshToken.Issue(s.Driver.Id, "hash", TestApp.Start);
        s.App.RefreshTokens.Add(token);

        await s.App.Sender.Send(new DeleteMyDriverAccountCommand());

        Assert.Equal(AccountStatus.Inactive, s.Driver.AccountStatus);
        Assert.Equal(OnlineStatus.Offline, s.Profile!.OnlineStatus);
        Assert.Null(s.Driver.FcmToken);
        Assert.NotNull(token.RevokedAt); // 9.1
        Assert.Equal(1, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task A_driver_who_never_applied_deletes_the_account()
    {
        var s = new Setup(withProfile: false);

        await s.App.Sender.Send(new DeleteMyDriverAccountCommand());

        Assert.Equal(AccountStatus.Inactive, s.Driver.AccountStatus);
    }

    [Fact]
    public async Task A_driver_with_a_running_order_cannot_delete_the_account()
    {
        var s = new Setup();
        s.RunningOrder();

        var ex = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new DeleteMyDriverAccountCommand()));

        Assert.Equal("ACCOUNT_HAS_OPEN_ORDERS", ex.Code);
        Assert.Equal(AccountStatus.Active, s.Driver.AccountStatus);
        Assert.Equal(0, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task A_driver_with_a_pending_withdrawal_cannot_delete_the_account()
    {
        var s = new Setup();
        s.Wallet!.CreditTripEarning(10, 100_000, 10_000);
        s.Wallet.RequestWithdrawal(90_000);

        var ex = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new DeleteMyDriverAccountCommand()));

        Assert.Equal("ACCOUNT_HAS_PENDING_WITHDRAWAL", ex.Code);
    }

    [Fact]
    public async Task A_driver_with_money_left_cannot_delete_the_account()
    {
        var s = new Setup();
        s.Wallet!.CreditTopUp(200_000);

        var ex = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new DeleteMyDriverAccountCommand()));

        Assert.Equal("ACCOUNT_HAS_WALLET_BALANCE", ex.Code);
        Assert.Equal(OnlineStatus.Online, s.Profile!.OnlineStatus);
    }

    [Theory]
    [InlineData(AccountStatus.Locked)]
    [InlineData(AccountStatus.Inactive)]
    public async Task A_locked_or_deleted_account_is_refused_B8(AccountStatus status)
    {
        var s = new Setup();
        s.Driver.ChangeStatus(status);

        var ex = await Assert.ThrowsAsync<ForbiddenAccessException>(() => s.App.Sender.Send(new DeleteMyDriverAccountCommand()));

        Assert.Equal("ACCOUNT_INACTIVE", ex.Code);
    }

    [Fact]
    public async Task Not_signed_in_is_401()
    {
        var app = new TestApp();

        await Assert.ThrowsAsync<UnauthorizedException>(() => app.Sender.Send(new DeleteMyDriverAccountCommand()));
    }
}
