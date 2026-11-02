using System.Security.Cryptography;
using FluentValidation;
using Microsoft.Extensions.Logging;
using ModMove.Application.Common.Interfaces;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Auth;

/// <summary>Rules shared by the Auth use cases, so register/reset/change password validate the same way.</summary>
internal static class AuthRules
{
    public const int MinPasswordLength = 8;

    public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MinimumLength(MinPasswordLength)
            .MaximumLength(100)
            .Matches("[A-Za-z]").WithMessage("Password must contain a letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.");

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(100).EmailAddress();

    /// <summary>Vietnamese mobile number: 10 digits starting with 0 (fits users.phone_number VARCHAR(15)).</summary>
    public static IRuleBuilderOptions<T, string> ValidPhone<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Matches("^0[0-9]{9}$").WithMessage("Enter a valid phone number (10 digits starting with 0).");

    /// <summary>Six random digits from a cryptographic source.</summary>
    public static string NewOtpCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

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
