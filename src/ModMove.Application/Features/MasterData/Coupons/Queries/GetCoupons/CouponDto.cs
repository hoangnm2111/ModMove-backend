namespace ModMove.Application.Features.MasterData.Coupons.Queries.GetCoupons;

public sealed record CouponDto
{
    public long Id { get; init; }
    public string DiscountCode { get; init; } = string.Empty;
    public decimal DiscountValue { get; init; }
    public decimal MinimumOrderValue { get; init; }
    public DateTimeOffset ValidFrom { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public int? UsageLimit { get; init; }
    public int UsedCount { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAt { get; init; }

    public CouponDto() { }

    public CouponDto(
        long id,
        string discountCode,
        decimal discountValue,
        decimal minimumOrderValue,
        DateTimeOffset validFrom,
        DateTimeOffset expiresAt,
        int? usageLimit,
        int usedCount,
        bool isActive,
        DateTimeOffset createdAt)
    {
        Id = id;
        DiscountCode = discountCode;
        DiscountValue = discountValue;
        MinimumOrderValue = minimumOrderValue;
        ValidFrom = validFrom;
        ExpiresAt = expiresAt;
        UsageLimit = usageLimit;
        UsedCount = usedCount;
        IsActive = isActive;
        CreatedAt = createdAt;
    }
}
