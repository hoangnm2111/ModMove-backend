using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Orders.Commands.CancelOrder;

/// <summary>
/// UC-CUS-29: the customer cancels while the order is Pending or Accepted, free of charge (DECISIONS 2.1, B3).
/// A paid PayOS order gets a 100% Cancellation refund that goes straight to an Administrator (B9).
/// <see cref="ReasonCode"/> comes from api/public/lookups/cancellation-reasons; <see cref="Reason"/> is required for OTHER.
/// </summary>
public sealed record CancelOrderCommand(long OrderId, string ReasonCode, string? Reason = null) : ICommand;

/// <summary>Body of POST api/customer/orders/{id}/cancel; the id comes from the route.</summary>
public sealed record CancelOrderRequest(string ReasonCode, string? Reason = null);
