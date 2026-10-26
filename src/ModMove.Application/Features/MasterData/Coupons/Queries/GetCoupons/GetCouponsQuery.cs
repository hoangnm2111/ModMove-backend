using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;

namespace ModMove.Application.Features.MasterData.Coupons.Queries.GetCoupons;

/// <summary>Lists coupons with pagination and optional active filter (UC-ADM-18).</summary>
public sealed record GetCouponsQuery(int Page = 1, int PageSize = 20, bool? IsActive = null)
    : IQuery<PagedResult<CouponDto>>;
