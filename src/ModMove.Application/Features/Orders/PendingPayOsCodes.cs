using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;

namespace ModMove.Application.Features.Orders;

/// <summary>DECISIONS 3.1: an order that is cancelled keeps no live PayOS code, neither on the database nor at PayOS.</summary>
internal static class PendingPayOsCodes
{
    public static async Task CancelForOrderAsync(
        long orderId, IPaymentTransactionRepository payments, IPaymentGateway? gateway, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        foreach (var pending in await payments.GetPendingPayOsForOrderAsync(orderId, cancellationToken))
        {
            pending.Cancel();
            // An expired code is already dead at PayOS; only a code still inside its 10 minutes needs cancelling there.
            if (gateway is not null && pending.PayosOrderCode is { } code && pending.ExpiresAt > now)
                await gateway.CancelPaymentLinkAsync(code, cancellationToken);
        }
    }
}
