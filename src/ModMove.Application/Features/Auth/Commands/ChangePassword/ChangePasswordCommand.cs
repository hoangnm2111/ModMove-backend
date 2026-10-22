using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Auth.Commands.ChangePassword;

/// <summary>UC-CUS-04 / UC-ADM-03: the signed-in user (any role) changes their own password.</summary>
public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : ICommand;
