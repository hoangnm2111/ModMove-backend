using ModMove.Domain.Entities;

namespace ModMove.Application.Common.Interfaces.Persistence;

/// <summary>Write-side access to payment attempts.</summary>
public interface IPaymentTransactionRepository : IRepository<PaymentTransaction>
{
    /// <summary>The order's PayOS sessions still Pending. A new code replaces them (DECISIONS 3.1).</summary>
    Task<IReadOnlyList<PaymentTransaction>> GetPendingPayOsForOrderAsync(long orderId, CancellationToken cancellationToken = default);

    /// <summary>The driver wallet's PayOS deposit sessions still Pending. A new top-up code replaces them (DECISIONS 5.2).</summary>
    /// <summary>The order's PayOS sessions that are not Paid yet (Pending, Cancelled, Expired, Failed), newest first: a transfer may still land on any of them.</summary>
    Task<IReadOnlyList<PaymentTransaction>> GetUnpaidPayOsForOrderAsync(long orderId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentTransaction>> GetPendingPayOsForWalletAsync(long walletId, CancellationToken cancellationToken = default);

    /// <summary>All PayOS sessions (order payment or wallet deposit) still in <see cref="Domain.Enums.PaymentTransactionStatus.Pending"/>, for JOB-PAY-01 (GB-13).</summary>
    Task<IReadOnlyList<PaymentTransaction>> GetPendingPayOsTransactionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Finds the PayOS session for a webhook by its unique <c>orderCode</c>.</summary>
    Task<PaymentTransaction?> GetByPayOsOrderCodeAsync(long payosOrderCode, CancellationToken cancellationToken = default);

    /// <summary>R-01: the order's latest <c>Paid</c> PayOS transaction that kept the payer's bank and account number; the account to offer as the refund account.</summary>
    Task<PaymentTransaction?> GetLatestPaidWithPayerAsync(long orderId, CancellationToken cancellationToken = default);
}

