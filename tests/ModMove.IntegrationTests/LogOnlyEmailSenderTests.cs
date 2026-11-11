using Microsoft.Extensions.Logging;
using ModMove.Application.Features.Auth.Commands.ForgotPassword;
using ModMove.Application.Features.Auth.Commands.Register;
using ModMove.Application.Features.Auth.Commands.VerifyEmail;
using ModMove.Infrastructure.ExternalServices.Email;

namespace ModMove.IntegrationTests;

/// <summary>Development without SMTP: the OTP is read from the console. Uses the real mails, so a new subject or body fails here.</summary>
public class LogOnlyEmailSenderTests
{
    private static readonly RegisterRequest Valid = new("An Nguyen", "0912345678", "an@example.com", "secret123");

    private static async Task<string> LogOf((string To, string Subject, string Body) mail)
    {
        var lines = new List<string>();
        await new LogOnlyEmailSender(new ListLogger(lines)).SendAsync(mail.To, mail.Subject, mail.Body, CancellationToken.None);
        return Assert.Single(lines);
    }

    [Fact]
    public async Task Register_code_is_logged_as_a_dev_otp_line()
    {
        var app = new TestApp();
        await app.Sender.Send(RegisterCommand.From(Valid, Domain.Enums.UserRole.Customer));

        var line = await LogOf(app.Email.Sent[^1]);

        Assert.Equal($"[DEV] Register OTP {app.Email.LastCode()} To: an@example.com", line);
    }

    [Fact]
    public async Task Reset_password_code_is_logged_as_a_dev_otp_line()
    {
        var app = new TestApp();
        await app.Sender.Send(RegisterCommand.From(Valid, Domain.Enums.UserRole.Customer));
        await app.Sender.Send(new VerifyEmailCommand(Valid.Email, app.Email.LastCode(), Valid.Password)); // the account exists only once verified
        await app.Sender.Send(new ForgotPasswordCommand(Valid.Email));

        var line = await LogOf(app.Email.Sent[^1]);

        Assert.Equal($"[DEV] ResetPassword OTP {app.Email.LastCode()} To: an@example.com", line);
    }

    [Fact]
    public async Task Other_mail_keeps_its_full_log_line()
    {
        var line = await LogOf(("an@example.com", "Tài khoản đã bị khóa", "<p>Nội dung</p>"));

        Assert.Equal("[DEV EMAIL, not sent] To: an@example.com | Subject: Tài khoản đã bị khóa | Body: <p>Nội dung</p>", line);
    }

    private sealed class ListLogger(List<string> lines) : ILogger<LogOnlyEmailSender>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            lines.Add(formatter(state, exception));
    }
}
