using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Matching.Commands.AcceptOrder;

/// <summary>
/// UC-DRV-10, GB-08..10, GB-16, GB-31, DECISIONS B5, B10, 3.2, 6.4:
/// the signed-in driver accepts a Pending order within the matching radius.
/// </summary>
public sealed record AcceptOrderCommand(long OrderId) : ICommand;

