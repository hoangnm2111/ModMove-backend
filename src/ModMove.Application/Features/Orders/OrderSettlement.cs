using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Orders;

/// <summary>
/// Settles the driver's wallet for an order that has just been completed, in the same transaction (GB-14, GB-15, M1).
/// Shared by the customer's confirmation (UC-CUS-19) and the auto-complete job (JOB-ORDER-01), so both settle alike.
/// The wallet entry is unique per (order, type) and the order's RowVersion is bumped on completion, so it settles once.
/// </summary>
internal sealed class OrderSettlement(IPlatformSettings settings, IDriverWalletRepository wallets)
{
    /// <summary>COMMISSION_RATE now; it cannot change while an order is running (DECISIONS C8).</summary>
    public Task<decimal> CommissionRateAsync(CancellationToken cancellationToken) =>
        settings.GetDecimalAsync(SettingKeys.CommissionRate, cancellationToken);

    public async Task SettleAsync(ShippingOrder order, CancellationToken cancellationToken)
    {
        if (order.OrderStatus != OrderStatus.Completed || order.CommissionAmount is not { } commission)
            throw new InvalidOperationException($"Order {order.Id} must be completed before its wallet is settled.");
        var driverProfileId = order.DriverProfileId
            ?? throw new InvalidOperationException($"Completed order {order.Id} has no driver.");
        // Every approved driver has a wallet (created on the first approval), so a missing one is a data error.
        var wallet = await wallets.GetByDriverProfileIdAsync(driverProfileId, cancellationToken)
            ?? throw new InvalidOperationException($"Driver profile {driverProfileId} has no wallet.");

        if (order.PaymentMethod == PaymentMethod.PayOS)
        {
            // The platform holds the online payment and owes the driver total_fee - commission (M1).
            if (order.TotalFee > 0)
                wallet.CreditTripEarning(order.Id, order.TotalFee, commission);
        }
        else if (commission > 0)
        {
            // The driver collected the cash; the platform's commission comes out of the deposit (GB-15).
            wallet.DeductCommissionFromDeposit(order.Id, commission);
        }
    }
}
