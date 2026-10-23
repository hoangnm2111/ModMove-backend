using ModMove.Domain.Entities;

namespace ModMove.Application.Common.Interfaces.Persistence;

public interface ICouponRepository : IRepository<Coupon>
{
    /// <summary>Exact match on discount_code (unique). Validity is checked by Coupon.EnsureApplicable (GB-21).</summary>
    Task<Coupon?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
}
