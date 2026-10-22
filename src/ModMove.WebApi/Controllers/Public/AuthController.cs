using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ModMove.Application.Features.Auth.Commands.ForgotPassword;
using ModMove.Application.Features.Auth.Commands.Login;
using ModMove.Application.Features.Auth.Commands.Refresh;
using ModMove.Application.Features.Auth.Commands.Register;
using ModMove.Application.Features.Auth.Commands.ResendOtp;
using ModMove.Application.Features.Auth.Commands.ResetPassword;
using ModMove.Application.Features.Auth.Commands.VerifyEmail;
using ModMove.Domain.Enums;

namespace ModMove.WebApi.Controllers.Public;

/// <summary>Anonymous sign-in and sign-up. Rate limited per IP: guessing passwords or 6-digit codes must be slow.</summary>
[EnableRateLimiting(AuthRateLimit.Policy)]
public sealed class AuthController : ApiControllerBase
{
    /// <summary>Sign in with email and password. One endpoint for every role; the token carries the role.</summary>
    [HttpPost("login")]
    public Task<LoginResponse> Login(LoginCommand command, CancellationToken cancellationToken) =>
        Sender.Send(command, cancellationToken);

    /// <summary>New access + refresh token for a refresh token; the old refresh token stops working (DECISIONS 9.1).</summary>
    [HttpPost("refresh")]
    public Task<LoginResponse> Refresh(RefreshTokenCommand command, CancellationToken cancellationToken) =>
        Sender.Send(command, cancellationToken);

    /// <summary>Customer sign-up (web and app). Sends an email OTP; sign-in works once it is verified.</summary>
    [HttpPost("register")]
    public Task<RegisterResponse> Register(RegisterRequest request, CancellationToken cancellationToken) =>
        Sender.Send(RegisterCommand.From(request, UserRole.Customer), cancellationToken);

    /// <summary>Driver sign-up, called by the mobile app only (DECISIONS B6).</summary>
    [HttpPost("register-driver")]
    public Task<RegisterResponse> RegisterDriver(RegisterRequest request, CancellationToken cancellationToken) =>
        Sender.Send(RegisterCommand.From(request, UserRole.Driver), cancellationToken);

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailCommand command, CancellationToken cancellationToken)
    {
        await Sender.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>Send a new code. Returns 204 even for unknown emails, so it cannot be used to find accounts.</summary>
    [HttpPost("otp/resend")]
    public async Task<IActionResult> ResendOtp(ResendOtpCommand command, CancellationToken cancellationToken)
    {
        await Sender.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>Email a reset code. Returns 204 even for unknown emails.</summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        await Sender.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        await Sender.Send(command, cancellationToken);
        return NoContent();
    }
}
