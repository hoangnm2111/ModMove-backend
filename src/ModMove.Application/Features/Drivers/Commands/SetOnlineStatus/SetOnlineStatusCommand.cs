using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Drivers.Commands.SetOnlineStatus;

/// <summary>
/// UC-DRV-08, GB-03: the signed-in driver goes Online (shown new orders, B5) or Offline. Going Online needs an Approved
/// profile and an Active account. Going Offline never touches an order already in progress (DECISIONS 6.4): the driver
/// only stops seeing new ones. The driver is the caller from the token, never an id in the request.
/// </summary>
public sealed record SetOnlineStatusCommand(bool IsOnline) : ICommand;

