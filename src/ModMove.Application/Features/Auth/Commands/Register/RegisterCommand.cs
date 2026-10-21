using ModMove.Application.Common.Messaging;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Auth.Commands.Register;

/// <summary>Body clients send. It has no role: the endpoint decides it (DECISIONS B6).</summary>
public sealed record RegisterRequest(string FullName, string PhoneNumber, string Email, string Password);

/// <summary>
/// UC-GST-01. <see cref="Role"/> is set by the controller (register → Customer, register-driver → Driver),
/// never taken from the request body.
/// </summary>
public sealed record RegisterCommand(string FullName, string PhoneNumber, string Email, string Password, UserRole Role)
    : ICommand<RegisterResponse>
{
    public static RegisterCommand From(RegisterRequest r, UserRole role) => new(r.FullName, r.PhoneNumber, r.Email, r.Password, role);
}

/// <summary>The account exists but cannot sign in until the email OTP is verified (D1).</summary>
public sealed record RegisterResponse(long UserId, string Email);
