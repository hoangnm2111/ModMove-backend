using MediatR;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Features.Auth.Commands.Login;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.IntegrationTests;

/// <summary>Runs the real MediatR pipeline (validation + logging + handler) with in-memory fakes for the ports.</summary>
public class LoginTests
{
    private static readonly DateTimeOffset Now = TestApp.Start;

    private static (ISender Sender, CountingUnitOfWork Uow) Build(User user)
    {
        var app = new TestApp(null, user);
        return (app.Sender, app.UnitOfWork);
    }

    private static User NewUser(UserRole role = UserRole.Customer) =>
        User.Create("A", "0900000001", "a@x.vn", "h:secret", role, emailVerified: true);

    [Fact]
    public async Task Valid_credentials_return_a_token_for_the_users_role()
    {
        var (sender, uow) = Build(NewUser(UserRole.Driver));

        var response = await sender.Send(new LoginCommand("a@x.vn", "secret"));

        Assert.Equal("token-Driver", response.AccessToken);
        Assert.Equal(UserRole.Driver, response.Role);
        Assert.Equal(1, uow.Saves);
    }

    [Fact]
    public async Task Wrong_password_is_unauthorized_and_counts_as_a_failure_GB02()
    {
        var user = NewUser();
        var (sender, uow) = Build(user);

        await Assert.ThrowsAsync<UnauthorizedException>(() => sender.Send(new LoginCommand("a@x.vn", "nope")));

        Assert.Equal(1, user.FailedLoginCount);
        Assert.Equal(1, uow.Saves); // the failure is saved before the 401 is thrown
    }

    [Fact]
    public async Task Unknown_email_gets_the_same_unauthorized_answer()
    {
        var (sender, uow) = Build(NewUser());

        await Assert.ThrowsAsync<UnauthorizedException>(() => sender.Send(new LoginCommand("nobody@x.vn", "secret")));

        Assert.Equal(0, uow.Saves);
    }

    [Fact]
    public async Task Phone_number_in_login_is_the_same_401_as_unknown_email_A01()
    {
        var user = NewUser();
        var (sender, uow) = Build(user);

        // A-03: sign-in is by email only; the phone is used at registration, never to sign in.
        var byPhone = await Assert.ThrowsAsync<UnauthorizedException>(() => sender.Send(new LoginCommand("0900000001", "secret")));
        var unknown = await Assert.ThrowsAsync<UnauthorizedException>(() => sender.Send(new LoginCommand("nobody@x.vn", "secret")));

        Assert.Equal(unknown.Message, byPhone.Message);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Equal(0, uow.Saves);
    }

    [Fact]
    public async Task Five_failures_lock_the_account_even_for_the_right_password_GB02()
    {
        var user = NewUser();
        var (sender, _) = Build(user);

        for (var i = 0; i < 5; i++)
            await Assert.ThrowsAsync<UnauthorizedException>(() => sender.Send(new LoginCommand("a@x.vn", "nope")));

        Assert.True(user.IsLocked(Now));
        await Assert.ThrowsAsync<UnauthorizedException>(() => sender.Send(new LoginCommand("a@x.vn", "secret")));
    }

    [Fact]
    public async Task Locked_account_and_unknown_email_get_the_same_answer()
    {
        var user = NewUser();
        var (sender, _) = Build(user);
        for (var i = 0; i < 5; i++)
            await Assert.ThrowsAsync<UnauthorizedException>(() => sender.Send(new LoginCommand("a@x.vn", "nope")));

        var locked = await Assert.ThrowsAsync<UnauthorizedException>(() => sender.Send(new LoginCommand("a@x.vn", "secret")));
        var unknown = await Assert.ThrowsAsync<UnauthorizedException>(() => sender.Send(new LoginCommand("nobody@x.vn", "secret")));

        Assert.Equal(unknown.Message, locked.Message);
    }

    [Fact]
    public async Task Inactive_account_cannot_sign_in()
    {
        var user = NewUser();
        user.ChangeStatus(AccountStatus.Inactive);
        var (sender, _) = Build(user);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => sender.Send(new LoginCommand("a@x.vn", "secret")));
    }

    [Fact]
    public async Task Empty_email_fails_validation_before_reaching_the_handler()
    {
        var (sender, uow) = Build(NewUser());

        await Assert.ThrowsAsync<ValidationException>(() => sender.Send(new LoginCommand("", "secret")));

        Assert.Equal(0, uow.Saves);
    }
}
