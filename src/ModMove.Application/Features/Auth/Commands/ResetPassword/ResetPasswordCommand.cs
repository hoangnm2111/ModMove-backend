using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Auth.Commands.ResetPassword;

/// <summary>UC-CUS-02 step 2: the emailed code proves ownership of the email, then the new password is set.</summary>
public sealed record ResetPasswordCommand(string Email, string Code, string NewPassword) : ICommand;
