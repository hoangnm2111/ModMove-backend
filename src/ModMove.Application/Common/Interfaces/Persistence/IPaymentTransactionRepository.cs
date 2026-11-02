using ModMove.Domain.Entities;

namespace ModMove.Application.Common.Interfaces.Persistence;

/// <summary>Write-side access to payment attempts.</summary>
public interface IPaymentTransactionRepository : IRepository<PaymentTransaction>
{
    /// <summary>The order's PayOS sessions still Pending. A new code replaces them (DECISIONS 3.1).</summary>
    Task<IReadOnlyList<PaymentTransaction>> GetPendingPayOsForOrderAsync(long orderId, CancellationToken cancellationToken = default);

    /// <summary>Finds the PayOS session for a webhook by its unique <c>orderCode</c>.</summary>
    Task<PaymentTransaction?> GetByPayOsOrderCodeAsync(long payosOrderCode, CancellationToken cancellationToken = default);
}

