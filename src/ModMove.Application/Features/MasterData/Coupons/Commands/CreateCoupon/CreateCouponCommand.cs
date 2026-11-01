using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.MasterData.Coupons.Commands.CreateCoupon;

/// <summary>Creates a new discount code (GB-21, UC-ADM-18).</summary>
public sealed record CreateCouponCommand(
    string DiscountCode,
    decimal DiscountValue,
    decimal MinimumOrderValue,
    DateTimeOffset ValidFrom,
    DateTimeOffset ExpiresAt,
    int? UsageLimit = null) : ICommand<long>;
