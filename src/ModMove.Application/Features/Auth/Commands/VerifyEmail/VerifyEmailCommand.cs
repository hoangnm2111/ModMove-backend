using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Auth.Commands.VerifyEmail;

/// <summary>UC-GST-01 last step: the email OTP turns the account into a usable one (D1).</summary>
public sealed record VerifyEmailCommand(string Email, string Code) : ICommand;
