using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Users.Commands.DeleteMyAccount;

/// <summary>
/// UC-CUS-07 / DECISIONS B8: the account becomes Inactive (cannot sign in); orders and history stay.
/// Refused while an order is still open, so no delivery is left without its customer.
/// </summary>
public sealed record DeleteMyAccountCommand : ICommand;
