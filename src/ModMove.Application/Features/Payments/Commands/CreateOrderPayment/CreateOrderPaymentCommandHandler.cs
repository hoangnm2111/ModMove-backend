using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Constants;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Payments.Commands.CreateOrderPayment;

internal sealed class CreateOrderPaymentCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IShippingOrderRepository orders,
    IPaymentTransactionRepository payments,
    IPaymentGateway gateway,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<CreateOrderPaymentCommand, CreateOrderPaymentResponse>
{
    public async Task<CreateOrderPaymentResponse> Handle(CreateOrderPaymentCommand request, CancellationToken cancellationToken)
    {
        var customer = await currentUser.LoadUserAsync(users, cancellationToken);
        var order = await orders.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), request.OrderId);

        var now = clock.GetUtcNow();
        // ORDER_NOT_YOURS / ORDER_CANCELLED / already paid / cash order / past 30 minutes all come from the entity.
        order.EnsureCanStartPayOsPayment(customer.Id, now);

        var payosOrderCode = NewPayOsOrderCode(now);
        var expiresAt = now + BusinessRules.PayOsSessionLifetime; // GB-13
        var link = await gateway.CreatePaymentLinkAsync(
            payosOrderCode, order.TotalFee, OrderDescription(order.Id), request.Platform, expiresAt, cancellationToken);

        // DECISIONS 3.1: the new code replaces the old one. Cancel after the new link exists, so a PayOS outage
        // never leaves the customer with no valid code at all.
        foreach (var previous in await payments.GetPendingPayOsForOrderAsync(order.Id, cancellationToken))
        {
            previous.Cancel();
            // An expired code is already dead at PayOS; only a code still inside its 10 minutes needs cancelling there.
            if (previous.PayosOrderCode is { } oldCode && previous.ExpiresAt > now)
                await gateway.CancelPaymentLinkAsync(oldCode, cancellationToken);
        }

        var transaction = PaymentTransaction.ForOrderViaPayOs(order.Id, order.TotalFee, payosOrderCode, link.CheckoutUrl, now);
        payments.Add(transaction);
        await unitOfWork.SaveChangesAsync(cancellationToken); // payos_order_code is unique: a clash is a 409, the client retries

        return new CreateOrderPaymentResponse(transaction.Id, link.CheckoutUrl, link.QrCode, transaction.Amount, transaction.ExpiresAt!.Value);
    }

    /// <summary>
    /// PayOS limits descriptions to 9 characters when the customer's bank is not directly linked.
    /// <c>MM{orderId}</c> stays within 9 characters and uniquely identifies the order for reconciliation.
    /// </summary>
    internal static string OrderDescription(long orderId)
    {
        var text = $"MM{orderId}";
        return text.Length <= BusinessRules.PayOsMaxDescriptionLength
            ? text
            : $"MM{orderId % 10_000_000}";
    }

    /// <summary>
    /// PayOS wants a unique integer up to 2^53-1. Milliseconds since 1970 times 1,000 plus a random 0-999 is about 1.8e15,
    /// inside that range, and it does not run out or need a sequence.
    /// </summary>
    internal static long NewPayOsOrderCode(DateTimeOffset now) =>
        now.ToUnixTimeMilliseconds() * 1000 + Random.Shared.Next(1000);
}
