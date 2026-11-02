using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Payments.Commands.CreateOrderPayment;

/// <summary>
/// UC-CUS-27, DECISIONS 3.1, GB-13: starts the PayOS payment of an order. The code lives 10 minutes; calling again for the
/// same order (after it expired) makes a new code and the old one stops taking transfers. Only the PayOS webhook, with a
/// valid signature, ever marks the order Paid.
/// </summary>
public sealed record CreateOrderPaymentCommand(long OrderId, PaymentPlatform Platform) : ICommand<CreateOrderPaymentResponse>;

/// <summary>Body of POST api/customer/orders/{id}/payment; the id comes from the route.</summary>
public sealed record CreateOrderPaymentRequest(PaymentPlatform Platform);

public sealed record CreateOrderPaymentResponse(
    long TransactionId, string CheckoutUrl, string? QrCode, decimal Amount, DateTimeOffset ExpiresAt);

