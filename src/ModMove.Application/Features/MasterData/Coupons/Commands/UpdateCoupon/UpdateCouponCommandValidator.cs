using FluentValidation;

namespace ModMove.Application.Features.MasterData.Coupons.Commands.UpdateCoupon;

internal sealed class UpdateCouponCommandValidator : AbstractValidator<UpdateCouponCommand>
{
    public UpdateCouponCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.DiscountValue).GreaterThan(0).WithMessage("Discount value must be greater than 0.");
        RuleFor(x => x.MinimumOrderValue).GreaterThanOrEqualTo(0).WithMessage("Minimum order value cannot be negative.");
        RuleFor(x => x.ExpiresAt).GreaterThan(x => x.ValidFrom).WithMessage("Expiration time must be after valid from time.");
        RuleFor(x => x.UsageLimit).GreaterThan(0).When(x => x.UsageLimit.HasValue).WithMessage("Usage limit must be greater than 0.");
    }
}
