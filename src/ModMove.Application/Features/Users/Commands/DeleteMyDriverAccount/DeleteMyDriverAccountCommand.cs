using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Users.Commands.DeleteMyDriverAccount;

/// <summary>
/// DECISIONS Q12, B8: the driver's account becomes Inactive (cannot sign in); profile, vehicle, wallet and history stay and an
/// Administrator can reopen it. Refused while an order runs, a withdrawal waits, or the wallet holds or owes money.
/// </summary>
public sealed record DeleteMyDriverAccountCommand : ICommand;
