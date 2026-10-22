using System.Net;
using System.Net.Mail;
using ModMove.Application.Common.Interfaces;

namespace ModMove.Infrastructure.ExternalServices.Email;

/// <summary>Bound from the "Email" configuration section (values live in appsettings.Development.json / user secrets).</summary>
public sealed class EmailOptions
{
    public string Host { get; init; } = "";
    public int Port { get; init; } = 587;
    public string UserName { get; init; } = "";

    /// <summary>For Gmail: an App Password, never the account password.</summary>
    public string Password { get; init; } = "";

    public string FromAddress { get; init; } = "";
    public string FromName { get; init; } = "ModMove";
}

/// <summary>Gmail SMTP with STARTTLS (OTP codes, GB-25, and notification emails).</summary>
public sealed class SmtpEmailSender(EmailOptions options) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(options.FromAddress, options.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
        };
        message.To.Add(to);

        using var client = new SmtpClient(options.Host, options.Port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(options.UserName, options.Password),
        };
        await client.SendMailAsync(message, cancellationToken);
    }
}
