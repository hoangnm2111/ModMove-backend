using ModMove.Domain.Common;
using ModMove.Domain.Exceptions;

namespace ModMove.Domain.Entities;

/// <summary>Discount code loaded as seed data (GB-21).</summary>
public sealed class Coupon : BaseAuditableEntity, IAggregateRoot
{
    private Coupon() { }

    public string DiscountCode { get; private set; } = null!;

    /// <summary>VND off the order total.</summary>
    public decimal DiscountValue { get; private set; }

    public decimal MinimumOrderValue { get; private set; }
    public DateTimeOffset ValidFrom { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Null = unlimited.</summary>
    public int? UsageLimit { get; private set; }

    public int UsedCount { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static Coupon Create(
        string discountCode, decimal discountValue, decimal minimumOrderValue,
        DateTimeOffset validFrom, DateTimeOffset expiresAt, int? usageLimit) =>
        new()
        {
            DiscountCode = discountCode,
            DiscountValue = discountValue,
            MinimumOrderValue = minimumOrderValue,
            ValidFrom = validFrom,
            ExpiresAt = expiresAt,
            UsageLimit = usageLimit,
        };

    /// <summary>GB-21: minimum spend, valid dates and unused limit must all hold.</summary>
    public void EnsureApplicable(decimal orderValue, DateTimeOffset now)
    {
        if (!IsActive || now < ValidFrom || now > ExpiresAt)
            throw new DomainException("The coupon is not valid at this time.");
        if (UsageLimit is not null && UsedCount >= UsageLimit)
            throw new DomainException("The coupon has reached its usage limit.");
        if (orderValue < MinimumOrderValue)
            throw new DomainException("The order does not reach the minimum value of the coupon.");
    }

    /// <summary>Discount for an order value; never more than the order itself.</summary>
    public decimal DiscountFor(decimal orderValue) => Math.Min(DiscountValue, orderValue);

    public void MarkUsed() => UsedCount++;

    public void SetActive(bool isActive) => IsActive = isActive;
}
