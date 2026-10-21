using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.Application.Features.Auth;

internal static class OtpCheck
{
    public const string InvalidCode = "OTP_INVALID";

    public static DomainException Invalid() => new("The code is wrong or has expired. Request a new one.", InvalidCode);

    /// <summary>
    /// Returns the latest OTP for the email and purpose, marked verified, when <paramref name="code"/> matches it.
    /// A wrong guess is counted and saved before the error, so 5 wrong guesses kill the code even across many IPs.
    /// Wrong, expired, used and burnt codes all give the same error.
    /// </summary>
    public static async Task<OtpVerification> VerifyAsync(
        IOtpRepository otps, IUnitOfWork unitOfWork, IPasswordHasher hasher, string email, OtpPurpose purpose, string code,
        DateTimeOffset now, CancellationToken ct)
    {
        var otp = await otps.GetLatestAsync(email, purpose, ct);
        if (otp is null || !otp.IsUsable(now))
            throw Invalid();

        if (!hasher.Verify(code, otp.CodeHash))
        {
            otp.RegisterFailedAttempt();
            await unitOfWork.SaveChangesAsync(ct);
            throw Invalid();
        }

        otp.MarkVerified(now);
        return otp;
    }
}
