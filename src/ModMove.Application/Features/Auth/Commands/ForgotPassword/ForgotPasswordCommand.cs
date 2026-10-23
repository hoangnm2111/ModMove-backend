using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Auth.Commands.ForgotPassword;

/// <summary>UC-CUS-02 step 1: email a reset code. Always succeeds, so it never reveals whether the email exists.</summary>
public sealed record ForgotPasswordCommand(string Email) : ICommand;
