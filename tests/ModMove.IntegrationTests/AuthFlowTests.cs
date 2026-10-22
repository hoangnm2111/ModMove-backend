using ModMove.Application.Common.Exceptions;
using ModMove.Application.Features.Auth.Commands.ChangePassword;
using ModMove.Application.Features.Auth.Commands.ForgotPassword;
using ModMove.Application.Features.Auth.Commands.Login;
using ModMove.Application.Features.Auth.Commands.Logout;
using ModMove.Application.Features.Auth.Commands.Register;
using ModMove.Application.Features.Auth.Commands.ResendOtp;
using ModMove.Application.Features.Auth.Commands.ResetPassword;
using ModMove.Application.Features.Auth.Commands.VerifyEmail;
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
    public async Task Email_and_phone_must_be_unique_GB01(string email, string phone)
    {
        var app = await RegisteredAsync();

        await Assert.ThrowsAsync<ConflictException>(() =>
            app.Sender.Send(RegisterCommand.From(Valid with { Email = email, PhoneNumber = phone }, UserRole.Customer)));
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
    public async Task Logout_stops_push_notifications_to_the_device()
    {
        var user = User.Create("A", "0900000001", "a@x.vn", "h:x", UserRole.Driver, emailVerified: true);
        user.SetFcmToken("device-token");
        var app = TestApp.SignedInAs(user);

        await app.Sender.Send(new LogoutCommand());

        Assert.Null(user.FcmToken);
    }

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
}
