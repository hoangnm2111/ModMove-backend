using ModMove.Application.Common.Exceptions;
using ModMove.Application.Features.Auth.Commands.Login;
using ModMove.Application.Features.Users.Commands.DeleteMyAccount;
using ModMove.Application.Features.Users.Commands.UpdateFcmToken;
using ModMove.Application.Features.Users.Commands.UpdateMyProfile;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

/// <summary>Profile and account deletion through the real pipeline with fakes.</summary>
public class UsersTests
{
    private static User Customer(string phone = "0900000001") =>
        User.Create("An", phone, "an@x.vn", "h:secret123", UserRole.Customer, emailVerified: true);

    private static TestApp SignedIn(User user, params User[] others) => TestApp.SignedInAs(user, others);

    [Fact]
    public async Task Update_profile_changes_name_and_phone()
    {
        var user = Customer();
        var app = SignedIn(user);

        await app.Sender.Send(new UpdateMyProfileCommand(" An Tran ", "0911111111"));

        Assert.Equal("An Tran", user.FullName);
        Assert.Equal("0911111111", user.PhoneNumber);
    }

    [Fact]
    public async Task Phone_used_by_another_account_is_a_conflict_GB01()
    {
        var user = Customer();
        var other = User.Create("Binh", "0922222222", "binh@x.vn", "h:x", UserRole.Driver, emailVerified: true);
        var app = SignedIn(user, other);

        var error = await Assert.ThrowsAsync<ConflictException>(() => app.Sender.Send(new UpdateMyProfileCommand("An", "0922222222")));
        Assert.Equal("PHONE_TAKEN", error.Code);
        Assert.Equal("0900000001", user.PhoneNumber);
    }

    [Fact]
    public async Task Keeping_your_own_phone_is_not_a_conflict()
    {
        var user = Customer();
        var app = SignedIn(user);

        await app.Sender.Send(new UpdateMyProfileCommand("An Tran", "0900000001"));

        Assert.Equal("An Tran", user.FullName);
    }

    [Fact]
    public async Task Fcm_token_is_saved_for_push_notifications()
    {
        var user = Customer();
        var app = SignedIn(user);

        await app.Sender.Send(new UpdateFcmTokenCommand(" device-1 "));

        Assert.Equal("device-1", user.FcmToken);
    }

    [Fact]
    public async Task Deleted_account_is_inactive_and_cannot_sign_in_B8()
    {
        var user = Customer();
        user.SetFcmToken("device-1");
        var app = SignedIn(user);

        await app.Sender.Send(new DeleteMyAccountCommand());

        Assert.Equal(AccountStatus.Inactive, user.AccountStatus);
        Assert.Null(user.FcmToken);
        var error = await Assert.ThrowsAsync<ForbiddenAccessException>(() => app.Sender.Send(new LoginCommand("an@x.vn", "secret123")));
        Assert.Equal("ACCOUNT_INACTIVE", error.Code);
        Assert.Single(app.Users.Items); // soft delete: the row stays
    }

    [Fact]
    public async Task Account_with_open_orders_cannot_be_deleted()
    {
        var user = Customer();
        var app = SignedIn(user);
        app.Orders.CustomersWithOpenOrders.Add(user.Id);

        var error = await Assert.ThrowsAsync<DomainException>(() => app.Sender.Send(new DeleteMyAccountCommand()));

        Assert.Equal("ACCOUNT_HAS_OPEN_ORDERS", error.Code);
        Assert.Equal(AccountStatus.Active, user.AccountStatus);
    }

    [Fact]
    public async Task Account_with_an_open_refund_cannot_be_deleted_B8()
    {
        var user = Customer();
        user.SetFcmToken("device-1");
        var app = SignedIn(user);
        app.Refunds.CustomersWithOpenRefunds.Add(user.Id);

        var error = await Assert.ThrowsAsync<DomainException>(() => app.Sender.Send(new DeleteMyAccountCommand()));

        Assert.Equal("ACCOUNT_HAS_OPEN_REFUNDS", error.Code);
        Assert.Equal(AccountStatus.Active, user.AccountStatus);
        Assert.Equal("device-1", user.FcmToken);
    }

    [Fact]
    public async Task Another_customers_open_orders_do_not_block_deletion()
    {
        var user = Customer();
        var app = SignedIn(user);
        app.Orders.CustomersWithOpenOrders.Add(user.Id + 1);

        await app.Sender.Send(new DeleteMyAccountCommand());

        Assert.Equal(AccountStatus.Inactive, user.AccountStatus);
    }

    [Fact]
    public async Task Deleted_account_cannot_use_its_still_valid_token()
    {
        var user = Customer();
        var app = SignedIn(user);
        await app.Sender.Send(new DeleteMyAccountCommand());

        var error = await Assert.ThrowsAsync<ForbiddenAccessException>(() => app.Sender.Send(new UpdateFcmTokenCommand("device-1")));

        Assert.Equal("ACCOUNT_INACTIVE", error.Code);
        Assert.Null(user.FcmToken);
    }

    [Fact]
    public async Task A_device_token_moves_to_whoever_signed_in_last()
    {
        var previous = User.Create("Binh", "0922222222", "binh@x.vn", "h:x", UserRole.Customer, emailVerified: true);
        previous.SetFcmToken("shared-phone");
        var user = Customer();
        var app = SignedIn(user, previous);

        await app.Sender.Send(new UpdateFcmTokenCommand("shared-phone"));

        Assert.Equal("shared-phone", user.FcmToken);
        Assert.Null(previous.FcmToken);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Fcm_token_cannot_be_empty(string token)
    {
        var app = SignedIn(Customer());

        await Assert.ThrowsAsync<ValidationException>(() => app.Sender.Send(new UpdateFcmTokenCommand(token)));
    }

    [Fact]
    public async Task Fcm_token_longer_than_the_column_is_rejected()
    {
        var app = SignedIn(Customer());

        await Assert.ThrowsAsync<ValidationException>(() => app.Sender.Send(new UpdateFcmTokenCommand(new string('t', 256))));
    }
}
