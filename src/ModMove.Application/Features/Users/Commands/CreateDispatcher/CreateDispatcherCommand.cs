using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Users.Commands.CreateDispatcher;

/// <summary>
/// UC-ADM-07 / DECISIONS B7: Administrator provisions a Dispatcher account with an initial password.
/// The account is created with Role = Dispatcher, AccountStatus = Active, and EmailVerified = true.
/// </summary>
public sealed record CreateDispatcherCommand(
    string FullName,
    string PhoneNumber,
    string Email,
    string Password) : ICommand<long>;

