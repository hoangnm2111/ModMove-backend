using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Trips.Commands.CollectCash;

/// <summary>
/// UC-DRV-17: the assigned driver confirms collecting cash for a Cash order, recording a Paid cash PaymentTransaction
/// and marking the order Paid.
/// </summary>
public sealed record CollectCashCommand(long OrderId) : ICommand;

