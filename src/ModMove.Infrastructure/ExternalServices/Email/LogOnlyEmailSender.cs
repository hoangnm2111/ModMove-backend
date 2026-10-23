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
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        logger.LogWarning("[DEV EMAIL, not sent] To: {To} | Subject: {Subject} | Body: {Body}", to, subject, htmlBody);
        return Task.CompletedTask;
    }
}
