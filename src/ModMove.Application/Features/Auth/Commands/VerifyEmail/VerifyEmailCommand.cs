using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Auth.Commands.VerifyEmail;

/// <summary>
/// UC-GST-01 last step: the email OTP turns the account into a usable one (D1). The password proves the person
/// verifying also set the account's password, because registering again can replace it before this step (A-02).
/// </summary>
public sealed record VerifyEmailCommand(string Email, string Code, string Password) : ICommand;
