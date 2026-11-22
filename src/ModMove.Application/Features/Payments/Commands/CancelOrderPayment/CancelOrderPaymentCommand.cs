using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Payments.Commands.CancelOrderPayment;

/// <summary>
/// UC-CUS-27 4a: the customer closes the PayOS page without paying. Every Pending PayOS session of the order is cancelled,
/// here and at PayOS, so the old QR code stops taking transfers. The order itself stays Pending and Unpaid: the customer
/// may start a new payment (DECISIONS 3.1) or cancel the order; JOB-PAY-01 still cancels it 30 minutes after booking.
/// Calling it when nothing is pending does nothing (idempotent).
/// </summary>
public sealed record CancelOrderPaymentCommand(long OrderId) : ICommand;
