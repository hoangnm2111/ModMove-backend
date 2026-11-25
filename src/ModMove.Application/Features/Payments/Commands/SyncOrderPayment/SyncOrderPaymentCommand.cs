using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Payments.Commands.SyncOrderPayment;

/// <summary>
/// UC-CUS-27: the customer says "I paid" (or the app comes back from the PayOS page). The webhook is the normal way to learn of a
/// payment, but it may not arrive (tunnel down, URL not registered). So ask PayOS directly about the order's codes and, if one is paid
/// for the exact amount, settle it just like the webhook would. Nothing paid or PayOS unreachable → does nothing (idempotent);
/// the client reloads the order to see the result.
/// </summary>
public sealed record SyncOrderPaymentCommand(long OrderId) : ICommand;
