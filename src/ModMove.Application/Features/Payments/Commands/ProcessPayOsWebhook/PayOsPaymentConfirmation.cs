using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Notifications;
using ModMove.Domain.Constants;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Payments.Commands.ProcessPayOsWebhook;

/// <summary>Who paid at PayOS (all optional). Used for a later refund account suggestion (R-01).</summary>
internal sealed record PayOsPayer(string? BankBin, string? AccountNumber, string? AccountName);

/// <summary>
/// What happens once PayOS has confirmed a transfer, whoever learned of it: the webhook, or the customer's own
/// "I paid" check that asks PayOS directly (SyncOrderPayment). One implementation, so both settle alike.
/// </summary>
internal sealed class PayOsPaymentConfirmation(
    IPaymentGateway gateway,
    IPaymentTransactionRepository payments,
    IShippingOrderRepository orders,
    IDriverWalletRepository wallets,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    IDriverProfileRepository? driverProfiles,
    IPlatformSettings? settings,
    INotificationService? notifications,
    IRefundRepository? refunds)
{
    public async Task ConfirmPaidAsync(PaymentTransaction transaction, PayOsPayer payer, CancellationToken cancellationToken)
    {
        // Idempotent: a duplicate webhook for an already-Paid session is a no-op.
        // Late transfers arriving after a session was locally marked Expired or Cancelled still transition to Paid.
        var now = clock.GetUtcNow();
        if (!transaction.MarkPaid(now))
            return;

        // R-01: remember who paid, so a later refund can offer that account. The numbers never go to a log or a notification.
        transaction.RecordPayer(payer.BankBin, payer.AccountNumber, payer.AccountName);

        ShippingOrder? paidOrder = null;
        ShippingOrder? refundOrder = null;
        if (transaction.OrderId is { } orderId)
        {
            var order = await orders.GetByIdAsync(orderId, cancellationToken)
                ?? throw new NotFoundException(nameof(ShippingOrder), orderId);

            foreach (var other in await payments.GetPendingPayOsForOrderAsync(order.Id, cancellationToken))
            {
                if (other.Id == transaction.Id)
                    continue;

                other.Cancel();
                if (other.PayosOrderCode is { } otherCode && other.ExpiresAt > now)
                    await gateway.CancelPaymentLinkAsync(otherCode, cancellationToken);
            }

            if (order.OrderStatus == OrderStatus.Cancelled)
            {
                // Customer or JOB-PAY-01 cancelled the order before the bank transfer arrived:
                // record the payment and queue a 100% cancellation refund for Admin approval instead of failing with 422.
                var reason = !string.IsNullOrWhiteSpace(order.CancellationReason)
                    ? $"Thanh toán đến sau khi đơn đã hủy: {order.CancellationReason}"
                    : "Thanh toán đến sau khi đơn đã hủy.";
                refunds?.Add(Refund.Request(
                    order.Id,
                    incidentId: null,
                    RefundSourceType.Cancellation,
                    reason,
                    evidenceUrls: null,
                    transaction.Amount,
                    order.CancellationReasonCode));
                refundOrder = order;
            }
            else if (order.PaymentStatus == PaymentStatus.Paid)
            {
                // Customer paid twice across two different QR sessions for the same order: queue a 100% refund for the extra payment.
                refunds?.Add(Refund.Request(
                    order.Id,
                    incidentId: null,
                    RefundSourceType.Cancellation,
                    "Khách chuyển khoản trùng lần 2 cho cùng đơn hàng.",
                    evidenceUrls: null,
                    transaction.Amount,
                    order.CancellationReasonCode));
                refundOrder = order;
            }
            else
            {
                order.MarkPaid(now);
                paidOrder = order;
            }
        }
        else if (transaction.WalletId is { } walletId)
        {
            var wallet = await wallets.GetByIdAsync(walletId, cancellationToken)
                ?? throw new NotFoundException(nameof(DriverWallet), walletId);

            // The money is already received: credit it, then keep GB-32 (a deposit still short is refilled from earnings).
            wallet.CreditTopUp(transaction.Amount);
            if (wallet.EarningsBalance > 0 && settings is not null)
                wallet.RefillDepositFromEarnings(await settings.GetDecimalAsync(SettingKeys.RequiredDeposit, cancellationToken));

            foreach (var other in await payments.GetPendingPayOsForWalletAsync(wallet.Id, cancellationToken))
            {
                if (other.Id == transaction.Id)
                    continue;

                other.Cancel();
                if (other.PayosOrderCode is { } otherCode && other.ExpiresAt > now)
                    await gateway.CancelPaymentLinkAsync(otherCode, cancellationToken);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // R-01: a late or double payment is owed back; the money has nowhere to go until the customer gives a bank account.
        if (refundOrder is not null && refunds is not null && notifications is not null)
            await notifications.NotifyCustomerAsync(refundOrder, CustomerEvent.RefundBankInfoRequired, cancellationToken);

        // DECISIONS 6.4, 8.3: PayOS orders enter the matching pool only once PaidAt is set.
        // Notify eligible nearby online drivers immediately via FCM + SignalR.
        if (paidOrder is not null
            && notifications is not null
            && driverProfiles is not null
            && settings is not null)
        {
            await notifications.NotifyNearbyDriversOfNewOrderAsync(
                paidOrder, driverProfiles, wallets, orders, settings, now, cancellationToken);
        }
    }
}
