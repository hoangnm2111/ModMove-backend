using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.IntegrationTests;

internal static class TestOrders
{
    /// <summary>
    /// DECISIONS 5.4: a cash order paid by the sender is collected at the pickup point before loading, so setups that walk such an
    /// order past pickup collect it first. No-op for any other order.
    /// </summary>
    public static void CollectSenderCash(this ShippingOrder order, DateTimeOffset now)
    {
        if (order is { PaymentMethod: PaymentMethod.Cash, PayerRole: PayerRole.Sender, PaymentStatus: not PaymentStatus.Paid, TotalFee: > 0 })
            order.CollectCash(order.DriverProfileId!.Value, now);
    }
}
