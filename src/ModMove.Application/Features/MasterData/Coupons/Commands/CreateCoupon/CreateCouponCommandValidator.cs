using FluentValidation;

namespace ModMove.Application.Features.MasterData.Coupons.Commands.CreateCoupon;

public sealed class CreateCouponCommandValidator : AbstractValidator<CreateCouponCommand>
{
    public CreateCouponCommandValidator()
    {
        RuleFor(x => x.DiscountCode)
            .NotEmpty()
            .MaximumLength(30)
            .Matches(@"^[A-Za-z0-9_-]+$")
            .WithMessage("Discount code can only contain letters, numbers, hyphens and underscores.");

        RuleFor(x => x.DiscountValue)
            .GreaterThan(0)
            .WithMessage("Discount value must be greater than 0.");

        RuleFor(x => x.MinimumOrderValue)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Minimum order value cannot be negative.");

        RuleFor(x => x.ExpiresAt)
            .GreaterThan(x => x.ValidFrom)
            .WithMessage("Expiration time must be after valid from time.");

        RuleFor(x => x.UsageLimit)
            .GreaterThan(0)
            .When(x => x.UsageLimit.HasValue)
            .WithMessage("Usage limit must be greater than 0.");
    }
}
