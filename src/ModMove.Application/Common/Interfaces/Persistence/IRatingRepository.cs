using ModMove.Domain.Entities;

namespace ModMove.Application.Common.Interfaces.Persistence;

public interface IRatingRepository : IRepository<Rating>
{
    /// <summary>GB-20: one rating per order, never edited. The unique index on order_id catches a concurrent second one.</summary>
    Task<bool> ExistsForOrderAsync(long orderId, CancellationToken cancellationToken = default);
}
