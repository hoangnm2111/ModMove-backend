using ModMove.Domain.Entities;

namespace ModMove.Application.Common.Interfaces.Persistence;

/// <summary>Write-side access to orders.</summary>
public interface IShippingOrderRepository : IRepository<ShippingOrder>
{
    /// <summary>The order with its proof of delivery, for steps that read or change the pickup/delivery photos.</summary>
    Task<ShippingOrder?> GetWithProofAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>True while the customer has an order that is neither Completed nor Cancelled.</summary>
    Task<bool> HasOpenOrdersForCustomerAsync(long customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// True while any order is running: from Accepted up to, but not including, Completed (DECISIONS C8).
    /// Pending has no driver yet and Cancelled is over, so neither counts.
    /// </summary>
    Task<bool> HasOrdersInProgressAsync(CancellationToken cancellationToken = default);

    /// <summary>Orders created in [fromUtc, toUtc); numbers the next order code of the day (DECISIONS 9.5).</summary>
    Task<int> CountCreatedBetweenAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);
}
