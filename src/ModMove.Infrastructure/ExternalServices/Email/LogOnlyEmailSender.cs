using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ModMove.Application.Common.Interfaces;

namespace ModMove.Infrastructure.ExternalServices.Email;

/// <summary>
/// Development only, when no SMTP is configured: writes the email (OTP codes included) to the log so
/// teammates without Gmail credentials can still register and verify. Program.cs never registers it
/// outside Development, because it would leak OTPs into production logs.
/// </summary>
public sealed class LogOnlyEmailSender(ILogger<LogOnlyEmailSender> logger) : IEmailSender
{
    private static readonly Regex OtpCode = new("<b>([0-9]{6})</b>", RegexOptions.Compiled);

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        // The port only carries a subject and a body; the wording comes from AuthRules.SendOtpAsync.
        var purpose = subject.Contains("xác minh tài khoản") ? "Register"
            : subject.Contains("đặt lại mật khẩu") ? "ResetPassword"
            : null;
        var code = OtpCode.Match(htmlBody);

        if (purpose is not null && code.Success)
            logger.LogWarning("[DEV] {Purpose} OTP {Code} To: {To}", purpose, code.Groups[1].Value, to);
        else
            logger.LogWarning("[DEV EMAIL, not sent] To: {To} | Subject: {Subject} | Body: {Body}", to, subject, htmlBody);

        return Task.CompletedTask;
    }
}
