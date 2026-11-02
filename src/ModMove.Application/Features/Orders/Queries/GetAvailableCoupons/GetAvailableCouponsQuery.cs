using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;

namespace ModMove.Application.Features.Orders.Queries.GetAvailableCoupons;

/// <summary>UC-GST-14: coupons a new order could use now (GB-21), soonest to expire first.</summary>
public sealed record GetAvailableCouponsQuery(int Page = 1, int PageSize = 20) : IQuery<PagedResult<AvailableCouponResponse>>;

/// <summary><see cref="RemainingUses"/> is null when the coupon has no usage limit.</summary>
public sealed record AvailableCouponResponse(
    string DiscountCode, decimal DiscountValue, decimal MinimumOrderValue, DateTimeOffset ExpiresAt, int? RemainingUses);
