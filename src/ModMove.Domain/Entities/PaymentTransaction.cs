using ModMove.Domain.Common;
using ModMove.Domain.Constants;
using ModMove.Domain.Enums;

namespace ModMove.Domain.Entities;

/// <summary>
/// One payment attempt: PayOS or cash for an order, PayOS for a deposit top-up.
/// Exactly one of <see cref="OrderId"/> and <see cref="WalletId"/> is set.
/// </summary>
public sealed class PaymentTransaction : BaseAuditableEntity, IAggregateRoot
{
    private PaymentTransaction() { }

    public long? OrderId { get; private set; }
    public long? WalletId { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public PayerRole? PayerRole { get; private set; }
    public decimal Amount { get; private set; }

    /// <summary>Unique per PayOS session.</summary>
    public long? PayosOrderCode { get; private set; }

    public string? CheckoutUrl { get; private set; }
    public PaymentTransactionStatus TransactionStatus { get; private set; } = PaymentTransactionStatus.Pending;
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }

    // R-01: the account the customer paid from, as PayOS reports it in the webhook (counterAccount*). Only a hint for
    // the refund account; the customer still confirms it. Null when PayOS sends nothing usable.
    public string? PayerBankBin { get; private set; }
    public string? PayerAccountNumber { get; private set; }
    public string? PayerAccountName { get; private set; }

    /// <summary>R-01: a payer account the customer can be offered back as the refund account.</summary>
    public bool HasPayerAccount => PayerBankBin is not null && PayerAccountNumber is not null;

    /// <summary>
    /// R-01: keeps what PayOS reported about the payer. Empty, blank and the text "null" or "undefined" (PayOS sends them
    /// for a missing value) become null, so nothing meaningless is stored. Values are cut to the column sizes.
    /// </summary>
    public void RecordPayer(string? bankBin, string? accountNumber, string? accountName)
    {
        PayerBankBin = Clean(bankBin, 20);
        PayerAccountNumber = Clean(accountNumber, 19);
        PayerAccountName = Clean(accountName, 100);
    }

    private static string? Clean(string? value, int maxLength)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text)
            || text.Equals("null", StringComparison.OrdinalIgnoreCase)
            || text.Equals("undefined", StringComparison.OrdinalIgnoreCase))
            return null;

        return text.Length > maxLength ? text[..maxLength] : text;
    }

    public static PaymentTransaction ForOrderViaPayOs(long orderId, decimal amount, long payosOrderCode, string checkoutUrl, DateTimeOffset now) =>
        new()
        {
            OrderId = orderId,
            PaymentMethod = PaymentMethod.PayOS,
            Amount = amount,
            PayosOrderCode = payosOrderCode,
            CheckoutUrl = checkoutUrl,
            ExpiresAt = now + BusinessRules.PayOsSessionLifetime,
        };

    public static PaymentTransaction ForOrderInCash(long orderId, PayerRole payer, decimal amount, DateTimeOffset now) =>
        new()
        {
            OrderId = orderId,
            PaymentMethod = PaymentMethod.Cash,
            PayerRole = payer,
            Amount = amount,
            TransactionStatus = PaymentTransactionStatus.Paid,
            PaidAt = now,
        };

    public static PaymentTransaction ForDeposit(long walletId, decimal amount, long payosOrderCode, string checkoutUrl, DateTimeOffset now) =>
        new()
        {
            WalletId = walletId,
            PaymentMethod = PaymentMethod.PayOS,
            Amount = amount,
            PayosOrderCode = payosOrderCode,
            CheckoutUrl = checkoutUrl,
            ExpiresAt = now + BusinessRules.PayOsSessionLifetime,
        };

    /// <summary>
    /// Marks the transaction as paid when PayOS confirms the bank transfer succeeded.
    /// Idempotent: a repeated webhook for an already-Paid transaction returns false and changes nothing.
    /// Late transfers arriving after the session was locally marked Expired or Cancelled are still recorded
    /// as Paid so no real money transfer is ever silently dropped.
    /// </summary>
    public bool MarkPaid(DateTimeOffset now)
    {
        if (TransactionStatus == PaymentTransactionStatus.Paid)
            return false;

        TransactionStatus = PaymentTransactionStatus.Paid;
        PaidAt = now;
        return true;
    }

    public void MarkFailed()
    {
        if (TransactionStatus == PaymentTransactionStatus.Pending)
            TransactionStatus = PaymentTransactionStatus.Failed;
    }

    public void Cancel()
    {
        if (TransactionStatus == PaymentTransactionStatus.Pending)
            TransactionStatus = PaymentTransactionStatus.Cancelled;
    }

    /// <summary>GB-13: JOB-PAY-01 expires sessions still Pending after 10 minutes.</summary>
    public bool ExpireIfDue(DateTimeOffset now)
    {
        if (TransactionStatus != PaymentTransactionStatus.Pending || ExpiresAt is null || now < ExpiresAt)
            return false;

        TransactionStatus = PaymentTransactionStatus.Expired;
        return true;
    }
}
