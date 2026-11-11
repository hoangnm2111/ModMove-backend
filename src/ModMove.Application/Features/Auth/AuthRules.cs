using System.Security.Cryptography;
using FluentValidation;
using Microsoft.Extensions.Logging;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Auth;

/// <summary>Rules shared by the Auth use cases, so register/reset/change password validate the same way.</summary>
internal static class AuthRules
{
    public const int MinPasswordLength = 8;

    public const string AlreadyRegistered = "This email or phone number is already registered.";

    public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MinimumLength(MinPasswordLength)
            .MaximumLength(100)
            .Matches("[A-Za-z]").WithMessage("Password must contain a letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.");

    /// <summary>
    /// Printable ASCII only (A-02): the email columns use utf8mb4_unicode_ci, which ignores accents and invisible characters,
    /// so a lookalike address on another domain would find the real account and receive its codes.
    /// </summary>
    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(100).EmailAddress()
            .Must(email => email is null || email.Trim().All(c => c is > ' ' and < '\x7F'))
            .WithMessage("Email must contain only unaccented letters, digits and standard symbols.");

    /// <summary>Vietnamese mobile number: 10 digits starting with 0 (fits users.phone_number VARCHAR(15)).</summary>
    public static IRuleBuilderOptions<T, string> ValidPhone<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Matches("^0[0-9]{9}$").WithMessage("Enter a valid phone number (10 digits starting with 0).");

    /// <summary>Six random digits from a cryptographic source.</summary>
    public static string NewOtpCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    /// <summary>
    /// Starts a code or replaces the current one, used or not; null at the GB-25 limit, when nothing changes and nothing may be sent.
    /// One row per email and purpose (unique index): two first sends in parallel cannot both win, the loser gets 409.
    /// </summary>
    public static async Task<OtpVerification?> TryIssueOtpAsync(
        IOtpRepository otps, string email, OtpPurpose purpose, string codeHash, DateTimeOffset now, CancellationToken ct)
    {
        var latest = await otps.GetLatestAsync(email, purpose, ct);
        if (latest is not null)
            return latest.TryResend(codeHash, now) ? latest : null;

        var otp = OtpVerification.Issue(email, purpose, codeHash, now);
        otps.Add(otp);
        return otp;
    }

    /// <summary>
    /// The account or code is already saved, so a mail failure is only logged: the user asks again with otp/resend, and a
    /// known email answers like an unknown one. The time taken still differs; closing that needs sending in the background.
    /// </summary>
    public static async Task SendOtpAsync(IEmailSender email, string to, string code, OtpPurpose purpose, ILogger logger, CancellationToken ct)
    {
        var (subject, action) = purpose == OtpPurpose.Register
            ? ("ModMove - Mã xác minh tài khoản", "xác minh tài khoản")
            : ("ModMove - Mã đặt lại mật khẩu", "đặt lại mật khẩu");

        try
        {
            await email.SendAsync(to, subject,
                $"<p>Mã {action} của bạn là <b>{code}</b>.</p><p>Mã có hiệu lực trong 5 phút. Không chia sẻ mã này cho bất kỳ ai.</p>",
                ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "The {Purpose} code email could not be sent", purpose); // the code is never logged
        }
    }

    public static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
