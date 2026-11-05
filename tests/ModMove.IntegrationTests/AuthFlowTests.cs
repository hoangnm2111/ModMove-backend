using ModMove.Application.Common.Exceptions;
using ModMove.Application.Features.Auth.Commands.ChangePassword;
using ModMove.Application.Features.Auth.Commands.ForgotPassword;
using ModMove.Application.Features.Auth.Commands.Login;
using ModMove.Application.Features.Auth.Commands.Logout;
using ModMove.Application.Features.Auth.Commands.Refresh;
using ModMove.Application.Features.Auth.Commands.Register;
using ModMove.Application.Features.Auth.Commands.ResendOtp;
using ModMove.Application.Features.Auth.Commands.ResetPassword;
using ModMove.Application.Features.Auth.Commands.VerifyEmail;
using ModMove.Application.Features.Users.Commands.DeleteMyAccount;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

/// <summary>Registration, OTP and password flows through the real pipeline (validators + handlers) with fakes.</summary>
public class AuthFlowTests
{
    private static readonly RegisterRequest Valid = new("An Nguyen", "0912345678", "An@Example.com", "secret123");

    private static async Task<TestApp> RegisteredAsync(UserRole role = UserRole.Customer)
    {
        var app = new TestApp();
        await app.Sender.Send(RegisterCommand.From(Valid, role));
        return app;
    }

    [Theory]
    [InlineData(UserRole.Customer)]
    [InlineData(UserRole.Driver)]
    public async Task Register_creates_an_unverified_account_with_the_role_the_endpoint_chose_B6(UserRole role)
    {
        var app = await RegisteredAsync(role);

        var user = Assert.Single(app.Users.Items);
        Assert.Equal(role, user.Role);
        Assert.Equal("an@example.com", user.Email); // emails are stored lower-case
        Assert.False(user.EmailVerified);
        Assert.Equal("an@example.com", Assert.Single(app.Email.Sent).To);
        Assert.Matches("^[0-9]{6}$", app.Email.LastCode());
    }

    [Fact]
    public async Task Mail_failure_keeps_the_new_account_and_the_code_can_be_sent_again()
    {
        var app = new TestApp();
        app.Email.Fail = true;

        var result = await app.Sender.Send(RegisterCommand.From(Valid, UserRole.Customer));

        Assert.Equal("an@example.com", result.Email);
        Assert.Single(app.Users.Items);
        Assert.Empty(app.Email.Sent);

        app.Email.Fail = false;
        await app.Sender.Send(new ResendOtpCommand(Valid.Email, OtpPurpose.Register));
        Assert.Matches("^[0-9]{6}$", app.Email.LastCode());
    }

    [Fact]
    public async Task Mail_failure_on_a_known_email_answers_like_an_unknown_one()
    {
        var app = await RegisteredAsync();
        app.Email.Fail = true;

        await app.Sender.Send(new ForgotPasswordCommand(Valid.Email));
        await app.Sender.Send(new ResendOtpCommand(Valid.Email, OtpPurpose.Register));
    }

    [Fact]
    public async Task Client_leaving_during_the_mail_is_not_logged_as_a_mail_failure()
    {
        var app = new TestApp();
        app.Email.Fail = true;
        using var left = new CancellationTokenSource();
        app.UnitOfWork.AfterSave = left.Cancel;

        await Assert.ThrowsAsync<InvalidOperationException>(() => app.Sender.Send(RegisterCommand.From(Valid, UserRole.Customer), left.Token));
    }

    [Theory]
    [InlineData(UserRole.Dispatcher)]
    [InlineData(UserRole.Administrator)]
    public async Task Staff_roles_cannot_self_register_B7(UserRole role)
    {
        var app = new TestApp();

        await Assert.ThrowsAsync<ValidationException>(() => app.Sender.Send(RegisterCommand.From(Valid, role)));
    }

    [Theory]
    [InlineData("short1")]       // under 8 characters
    [InlineData("onlyletters")]  // no digit
    [InlineData("12345678")]     // no letter
    public async Task Weak_password_is_rejected(string password)
    {
        var app = new TestApp();

        await Assert.ThrowsAsync<ValidationException>(() =>
            app.Sender.Send(RegisterCommand.From(Valid with { Password = password }, UserRole.Customer)));
    }

    [Theory]
    [InlineData("an@example.com", "0999999999")] // same email, different case
    [InlineData("other@example.com", "0912345678")] // same phone
    public async Task Email_and_phone_of_a_verified_account_stay_taken_GB01(string email, string phone)
    {
        var app = await RegisteredAsync();
        app.Users.Items[0].VerifyEmail();

        await Assert.ThrowsAsync<ConflictException>(() =>
            app.Sender.Send(RegisterCommand.From(Valid with { Email = email, PhoneNumber = phone }, UserRole.Customer)));
    }

    [Fact]
    public async Task Registering_again_before_verifying_sends_a_new_code_and_the_latest_details_win_A02()
    {
        var app = await RegisteredAsync();
        var firstCode = app.Email.LastCode();

        var again = await app.Sender.Send(RegisterCommand.From(
            Valid with { FullName = "An Tran", PhoneNumber = "0999999999", Password = "newsecret123" }, UserRole.Customer));

        var user = Assert.Single(app.Users.Items);
        Assert.Equal((user.Id, "an@example.com"), (again.UserId, again.Email));
        Assert.Equal(("An Tran", "0999999999"), (user.FullName, user.PhoneNumber));
        Assert.Equal(2, app.Email.Sent.Count);
        Assert.Single(app.Otps.Items);

        if (firstCode != app.Email.LastCode())
            await Assert.ThrowsAsync<DomainException>(() => app.Sender.Send(new VerifyEmailCommand(Valid.Email, firstCode)));
        await app.Sender.Send(new VerifyEmailCommand(Valid.Email, app.Email.LastCode()));
        await app.Sender.Send(new LoginCommand(Valid.Email, "newsecret123"));
        await Assert.ThrowsAsync<UnauthorizedException>(() => app.Sender.Send(new LoginCommand(Valid.Email, Valid.Password)));
    }

    [Fact]
    public async Task Registering_again_shares_the_send_limit_and_at_the_limit_changes_nothing_GB25()
    {
        var app = await RegisteredAsync(); // email 1
        await app.Sender.Send(new ResendOtpCommand(Valid.Email, OtpPurpose.Register)); // email 2
        await app.Sender.Send(RegisterCommand.From(Valid, UserRole.Customer)); // email 3

        var result = await app.Sender.Send(RegisterCommand.From(
            Valid with { FullName = "An Tran", PhoneNumber = "0999999999", Password = "newsecret123" }, UserRole.Customer));

        Assert.Equal("an@example.com", result.Email);
        Assert.Equal(3, app.Email.Sent.Count);
        Assert.Equal(("An Nguyen", "0912345678"), (app.Users.Items[0].FullName, app.Users.Items[0].PhoneNumber));

        await app.Sender.Send(new VerifyEmailCommand(Valid.Email, app.Email.LastCode()));
        await app.Sender.Send(new LoginCommand(Valid.Email, Valid.Password));
    }

    [Theory]
    [InlineData(AccountStatus.Inactive)]
    [InlineData(AccountStatus.Locked)]
    public async Task Closed_or_locked_account_cannot_register_again(AccountStatus status)
    {
        var app = await RegisteredAsync();
        app.Users.Items[0].ChangeStatus(status);

        await Assert.ThrowsAsync<ConflictException>(() => app.Sender.Send(RegisterCommand.From(Valid, UserRole.Customer)));

        Assert.Single(app.Email.Sent);
    }

    [Fact]
    public async Task Registering_again_with_another_role_stays_taken_B6()
    {
        var app = await RegisteredAsync();

        await Assert.ThrowsAsync<ConflictException>(() => app.Sender.Send(RegisterCommand.From(Valid, UserRole.Driver)));

        Assert.Equal(UserRole.Customer, app.Users.Items[0].Role);
        Assert.Single(app.Email.Sent);
    }

    [Fact]
    public async Task Registering_again_cannot_take_the_phone_of_another_account_GB01()
    {
        var app = await RegisteredAsync();
        await app.Sender.Send(RegisterCommand.From(Valid with { Email = "other@example.com", PhoneNumber = "0999999999" }, UserRole.Customer));

        await Assert.ThrowsAsync<ConflictException>(() =>
            app.Sender.Send(RegisterCommand.From(Valid with { PhoneNumber = "0999999999" }, UserRole.Customer)));

        Assert.Equal("0912345678", app.Users.Items[0].PhoneNumber);
        Assert.Equal(2, app.Email.Sent.Count);
    }

    [Fact]
    public async Task New_email_cannot_take_the_phone_of_an_unverified_account_GB01()
    {
        var app = await RegisteredAsync();

        await Assert.ThrowsAsync<ConflictException>(() =>
            app.Sender.Send(RegisterCommand.From(Valid with { Email = "other@example.com" }, UserRole.Customer)));
    }

    [Fact]
    public async Task Right_code_verifies_the_email_and_then_login_works_D1()
    {
        var app = await RegisteredAsync();

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => app.Sender.Send(new LoginCommand(Valid.Email, Valid.Password)));

        await app.Sender.Send(new VerifyEmailCommand(Valid.Email, app.Email.LastCode()));

        Assert.True(app.Users.Items[0].EmailVerified);
        var login = await app.Sender.Send(new LoginCommand(Valid.Email, Valid.Password));
        Assert.Equal(UserRole.Customer, login.Role);
    }

    [Fact]
    public async Task Login_before_verification_says_EMAIL_NOT_VERIFIED_only_with_the_right_password()
    {
        var app = await RegisteredAsync();

        var notVerified = await Assert.ThrowsAsync<ForbiddenAccessException>(() => app.Sender.Send(new LoginCommand(Valid.Email, Valid.Password)));
        Assert.Equal("EMAIL_NOT_VERIFIED", notVerified.Code);
        await Assert.ThrowsAsync<UnauthorizedException>(() => app.Sender.Send(new LoginCommand(Valid.Email, "wrong1234")));
    }

    [Fact]
    public async Task Wrong_or_expired_code_is_rejected_GB25()
    {
        var app = await RegisteredAsync();
        var code = app.Email.LastCode();
        var wrong = code == "000000" ? "111111" : "000000";

        var error = await Assert.ThrowsAsync<DomainException>(() => app.Sender.Send(new VerifyEmailCommand(Valid.Email, wrong)));
        Assert.Equal("OTP_INVALID", error.Code);

        app.Time.Now = TestApp.Start.AddMinutes(5); // codes live 5 minutes
        await Assert.ThrowsAsync<DomainException>(() => app.Sender.Send(new VerifyEmailCommand(Valid.Email, code)));
        Assert.False(app.Users.Items[0].EmailVerified);
    }

    [Fact]
    public async Task A_code_works_only_once()
    {
        var app = new TestApp(null, User.Create("A", "0900000001", "a@x.vn", "h:old12345", UserRole.Customer, emailVerified: true));
        await app.Sender.Send(new ForgotPasswordCommand("a@x.vn"));
        var code = app.Email.LastCode();

        await app.Sender.Send(new ResetPasswordCommand("a@x.vn", code, "new12345"));

        await Assert.ThrowsAsync<DomainException>(() => app.Sender.Send(new ResetPasswordCommand("a@x.vn", code, "again123")));
    }

    [Fact]
    public async Task Resend_allows_three_emails_per_15_minutes_GB25()
    {
        var app = await RegisteredAsync(); // email 1
        await app.Sender.Send(new ResendOtpCommand(Valid.Email, OtpPurpose.Register)); // email 2
        await app.Sender.Send(new ResendOtpCommand(Valid.Email, OtpPurpose.Register)); // email 3

        await app.Sender.Send(new ResendOtpCommand(Valid.Email, OtpPurpose.Register)); // over the limit: silent, no 422
        Assert.Equal(3, app.Email.Sent.Count);

        app.Time.Now = TestApp.Start.AddMinutes(15); // a new window opens
        await app.Sender.Send(new ResendOtpCommand(Valid.Email, OtpPurpose.Register));
        Assert.Equal(4, app.Email.Sent.Count);
    }

    [Fact]
    public async Task Only_the_newest_code_counts_after_a_resend()
    {
        var app = await RegisteredAsync();
        var first = app.Email.LastCode();
        await app.Sender.Send(new ResendOtpCommand(Valid.Email, OtpPurpose.Register));
        var second = app.Email.LastCode();

        if (first != second)
            await Assert.ThrowsAsync<DomainException>(() => app.Sender.Send(new VerifyEmailCommand(Valid.Email, first)));
        await app.Sender.Send(new VerifyEmailCommand(Valid.Email, second));
        Assert.True(app.Users.Items[0].EmailVerified);
    }

    [Fact]
    public async Task Forgot_password_for_an_unknown_email_succeeds_silently()
    {
        var app = new TestApp();

        await app.Sender.Send(new ForgotPasswordCommand("nobody@x.vn"));

        Assert.Empty(app.Email.Sent);
    }

    [Fact]
    public async Task Reset_password_sets_the_new_password_and_lifts_the_lock()
    {
        var user = User.Create("A", "0900000001", "a@x.vn", "h:old12345", UserRole.Customer, emailVerified: true);
        for (var i = 0; i < 5; i++)
            user.RegisterFailedLogin(TestApp.Start); // locked (GB-02)
        var app = new TestApp(null, user);

        await app.Sender.Send(new ForgotPasswordCommand("a@x.vn"));
        await app.Sender.Send(new ResetPasswordCommand("a@x.vn", app.Email.LastCode(), "new12345"));

        var login = await app.Sender.Send(new LoginCommand("a@x.vn", "new12345"));
        Assert.Equal(UserRole.Customer, login.Role);
        await Assert.ThrowsAsync<UnauthorizedException>(() => app.Sender.Send(new LoginCommand("a@x.vn", "old12345")));
    }

    [Fact]
    public async Task Change_password_needs_the_current_one()
    {
        var user = User.Create("A", "0900000001", "a@x.vn", "h:old12345", UserRole.Dispatcher, emailVerified: true);
        var app = TestApp.SignedInAs(user);

        var wrong = await Assert.ThrowsAsync<DomainException>(() => app.Sender.Send(new ChangePasswordCommand("nope1234", "new12345")));
        Assert.Equal("WRONG_PASSWORD", wrong.Code);

        await app.Sender.Send(new ChangePasswordCommand("old12345", "new12345"));
        Assert.Equal("h:new12345", user.PasswordHash);
    }

    [Fact]
    public async Task Change_password_without_a_signed_in_user_is_unauthorized()
    {
        var app = new TestApp(signedInUserId: null);

        await Assert.ThrowsAsync<UnauthorizedException>(() => app.Sender.Send(new ChangePasswordCommand("old12345", "new12345")));
    }

    [Fact]
    public async Task Logout_on_the_app_stops_pushes_to_that_device_9_2()
    {
        var user = User.Create("A", "0900000001", "a@x.vn", "h:x", UserRole.Driver, emailVerified: true);
        user.SetFcmToken("device-token");
        var app = TestApp.SignedInAs(user);

        await app.Sender.Send(new LogoutCommand("device-token"));

        Assert.Null(user.FcmToken);
    }

    [Fact]
    public async Task Logout_on_an_old_device_keeps_pushes_to_the_newer_one_9_2()
    {
        var user = User.Create("A", "0900000001", "a@x.vn", "h:x", UserRole.Driver, emailVerified: true);
        user.SetFcmToken("new-phone");
        var app = TestApp.SignedInAs(user);

        await app.Sender.Send(new LogoutCommand("old-phone"));

        Assert.Equal("new-phone", user.FcmToken);
    }

    [Fact]
    public async Task Logout_on_the_web_leaves_the_app_token_alone_9_2()
    {
        var user = User.Create("A", "0900000001", "a@x.vn", "h:x", UserRole.Customer, emailVerified: true);
        user.SetFcmToken("phone");
        var app = TestApp.SignedInAs(user);

        await app.Sender.Send(new LogoutCommand());

        Assert.Equal("phone", user.FcmToken);
        Assert.Equal(1, app.UnitOfWork.Saves);
    }

    [Fact]
    public async Task An_empty_token_clears_nothing_but_still_ends_every_session_9_1()
    {
        var user = User.Create("A", "0900000001", "a@x.vn", "h:secret123", UserRole.Driver, emailVerified: true);
        user.SetFcmToken("phone");
        var app = TestApp.SignedInAs(user);
        var login = await app.Sender.Send(new LoginCommand("a@x.vn", "secret123"));

        await app.Sender.Send(new LogoutCommand(""));

        Assert.Equal("phone", user.FcmToken);
        await Assert.ThrowsAsync<UnauthorizedException>(() => app.Sender.Send(new RefreshTokenCommand(login.RefreshToken)));
    }

    [Fact]
    public async Task Logout_token_is_at_most_255_characters() =>
        await Assert.ThrowsAsync<ValidationException>(() =>
            TestApp.SignedInAs(User.Create("A", "0900000001", "a@x.vn", "h:x", UserRole.Driver, emailVerified: true))
                .Sender.Send(new LogoutCommand(new string('x', 256))));

    [Fact]
    public async Task Five_wrong_guesses_burn_the_code_even_if_the_sixth_is_right()
    {
        var app = new TestApp(null, User.Create("A", "0900000001", "a@x.vn", "h:old12345", UserRole.Customer, emailVerified: true));
        await app.Sender.Send(new ForgotPasswordCommand("a@x.vn"));
        var code = app.Email.LastCode();
        var wrong = code == "000000" ? "111111" : "000000";

        for (var i = 0; i < 5; i++)
            await Assert.ThrowsAsync<DomainException>(() => app.Sender.Send(new ResetPasswordCommand("a@x.vn", wrong, "new12345")));

        await Assert.ThrowsAsync<DomainException>(() => app.Sender.Send(new ResetPasswordCommand("a@x.vn", code, "new12345")));
        Assert.Equal("h:old12345", app.Users.Items[0].PasswordHash);
        Assert.Equal(1 + 5, app.UnitOfWork.Saves); // issuing the code, then each wrong guess saved before its error
    }

    [Fact]
    public async Task A_register_code_cannot_reset_a_password()
    {
        var app = await RegisteredAsync();

        await Assert.ThrowsAsync<DomainException>(() =>
            app.Sender.Send(new ResetPasswordCommand(Valid.Email, app.Email.LastCode(), "new12345")));
    }

    [Fact]
    public async Task Resend_for_an_already_verified_account_sends_nothing()
    {
        var app = await RegisteredAsync();
        await app.Sender.Send(new VerifyEmailCommand(Valid.Email, app.Email.LastCode()));

        await app.Sender.Send(new ResendOtpCommand(Valid.Email, OtpPurpose.Register));

        Assert.Single(app.Email.Sent);
    }

    [Fact]
    public async Task Verify_for_an_unknown_email_or_a_verified_account_gives_the_same_error()
    {
        var app = await RegisteredAsync();
        var code = app.Email.LastCode();
        await app.Sender.Send(new VerifyEmailCommand(Valid.Email, code));

        var verified = await Assert.ThrowsAsync<DomainException>(() => app.Sender.Send(new VerifyEmailCommand(Valid.Email, code)));
        var unknown = await Assert.ThrowsAsync<DomainException>(() => app.Sender.Send(new VerifyEmailCommand("nobody@x.vn", code)));

        Assert.Equal(unknown.Code, verified.Code);
        Assert.Equal("OTP_INVALID", unknown.Code);
    }

    [Fact]
    public async Task New_password_must_differ_from_the_current_one()
    {
        var user = User.Create("A", "0900000001", "a@x.vn", "h:old12345", UserRole.Customer, emailVerified: true);
        var app = TestApp.SignedInAs(user);

        await Assert.ThrowsAsync<ValidationException>(() => app.Sender.Send(new ChangePasswordCommand("old12345", "old12345")));
    }

    private static async Task<(TestApp App, LoginResponse Login)> SignedInAsync()
    {
        var app = new TestApp(null, User.Create("A", "0900000001", "a@x.vn", "h:secret123", UserRole.Customer, emailVerified: true));
        var login = await app.Sender.Send(new LoginCommand("a@x.vn", "secret123"));
        return (app, login);
    }

    [Fact]
    public async Task Login_returns_a_refresh_token_stored_only_as_a_hash_9_1()
    {
        var (app, login) = await SignedInAsync();

        Assert.False(string.IsNullOrEmpty(login.RefreshToken));
        var stored = Assert.Single(app.RefreshTokens.Items);
        Assert.NotEqual(login.RefreshToken, stored.TokenHash);
        Assert.Equal(TestApp.Start.AddDays(30), stored.ExpiresAt);
    }

    [Fact]
    public async Task Refresh_rotates_the_token_9_1()
    {
        var (app, login) = await SignedInAsync();

        var next = await app.Sender.Send(new RefreshTokenCommand(login.RefreshToken));

        Assert.NotEqual(login.RefreshToken, next.RefreshToken);
        Assert.Equal("token-Customer", next.AccessToken);
        Assert.NotNull(app.RefreshTokens.Items[0].RevokedAt); // the old one is used up
    }

    [Fact]
    public async Task Reusing_an_old_refresh_token_signs_the_user_out_everywhere_9_1()
    {
        var (app, login) = await SignedInAsync();
        var next = await app.Sender.Send(new RefreshTokenCommand(login.RefreshToken));

        await Assert.ThrowsAsync<UnauthorizedException>(() => app.Sender.Send(new RefreshTokenCommand(login.RefreshToken)));

        // the stolen copy is caught, so even the newest token stops working
        await Assert.ThrowsAsync<UnauthorizedException>(() => app.Sender.Send(new RefreshTokenCommand(next.RefreshToken)));
    }

    [Fact]
    public async Task Expired_refresh_token_is_rejected()
    {
        var (app, login) = await SignedInAsync();
        app.Time.Now = TestApp.Start.AddDays(30);

        await Assert.ThrowsAsync<UnauthorizedException>(() => app.Sender.Send(new RefreshTokenCommand(login.RefreshToken)));
    }

    [Fact]
    public async Task Unknown_refresh_token_is_rejected()
    {
        var (app, _) = await SignedInAsync();

        await Assert.ThrowsAsync<UnauthorizedException>(() => app.Sender.Send(new RefreshTokenCommand("made-up")));
    }

    [Fact]
    public async Task Changing_the_password_ends_every_session_9_1()
    {
        var user = User.Create("A", "0900000001", "a@x.vn", "h:secret123", UserRole.Customer, emailVerified: true);
        var app = TestApp.SignedInAs(user);
        var login = await app.Sender.Send(new LoginCommand("a@x.vn", "secret123"));

        await app.Sender.Send(new ChangePasswordCommand("secret123", "secret456"));

        await Assert.ThrowsAsync<UnauthorizedException>(() => app.Sender.Send(new RefreshTokenCommand(login.RefreshToken)));
    }

    [Fact]
    public async Task Logout_ends_every_session_9_1()
    {
        var user = User.Create("A", "0900000001", "a@x.vn", "h:secret123", UserRole.Customer, emailVerified: true);
        var app = TestApp.SignedInAs(user);
        var login = await app.Sender.Send(new LoginCommand("a@x.vn", "secret123"));

        await app.Sender.Send(new LogoutCommand());

        await Assert.ThrowsAsync<UnauthorizedException>(() => app.Sender.Send(new RefreshTokenCommand(login.RefreshToken)));
    }

    [Fact]
    public async Task A_stale_device_after_logout_does_not_end_the_new_session_9_1()
    {
        var user = User.Create("A", "0900000001", "a@x.vn", "h:secret123", UserRole.Customer, emailVerified: true);
        var app = TestApp.SignedInAs(user);
        var old = await app.Sender.Send(new LoginCommand("a@x.vn", "secret123"));
        await app.Sender.Send(new LogoutCommand());
        var fresh = await app.Sender.Send(new LoginCommand("a@x.vn", "secret123"));

        await Assert.ThrowsAsync<UnauthorizedException>(() => app.Sender.Send(new RefreshTokenCommand(old.RefreshToken)));

        var next = await app.Sender.Send(new RefreshTokenCommand(fresh.RefreshToken));
        Assert.False(string.IsNullOrEmpty(next.RefreshToken));
    }

    [Fact]
    public async Task Reset_password_ends_every_session_9_1()
    {
        var (app, login) = await SignedInAsync();

        await app.Sender.Send(new ForgotPasswordCommand("a@x.vn"));
        await app.Sender.Send(new ResetPasswordCommand("a@x.vn", app.Email.LastCode(), "new12345"));

        await Assert.ThrowsAsync<UnauthorizedException>(() => app.Sender.Send(new RefreshTokenCommand(login.RefreshToken)));
    }

    [Fact]
    public async Task Deleting_the_account_ends_every_session_B8()
    {
        var user = User.Create("A", "0900000001", "a@x.vn", "h:secret123", UserRole.Customer, emailVerified: true);
        var app = TestApp.SignedInAs(user);
        var login = await app.Sender.Send(new LoginCommand("a@x.vn", "secret123"));

        await app.Sender.Send(new DeleteMyAccountCommand());

        await Assert.ThrowsAsync<UnauthorizedException>(() => app.Sender.Send(new RefreshTokenCommand(login.RefreshToken)));
    }

    [Theory]
    [InlineData(AccountStatus.Inactive)]
    [InlineData(AccountStatus.Locked)]
    public async Task Refresh_is_refused_once_the_account_is_not_active(AccountStatus status)
    {
        var (app, login) = await SignedInAsync();
        app.Users.Items[0].ChangeStatus(status);

        await Assert.ThrowsAsync<UnauthorizedException>(() => app.Sender.Send(new RefreshTokenCommand(login.RefreshToken)));
    }
}
