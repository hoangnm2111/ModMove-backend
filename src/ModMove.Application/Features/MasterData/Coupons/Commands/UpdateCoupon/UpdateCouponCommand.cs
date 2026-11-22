using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.MasterData.Coupons.Commands.UpdateCoupon;

/// <summary>UC-ADM-18: edits the terms of a coupon; the code stays as it is (GB-21).</summary>
public sealed record UpdateCouponCommand(
    long Id,
    decimal DiscountValue,
    decimal MinimumOrderValue,
    DateTimeOffset ValidFrom,
    DateTimeOffset ExpiresAt,
    int? UsageLimit = null) : ICommand;
