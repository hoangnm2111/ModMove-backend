using FluentValidation;
using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Auth;
using ModMove.Domain.Constants;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Orders.Commands.CreateOrder;

internal sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator(IFileStorage storage)
    {
        RuleFor(x => x.VehicleTypeId).GreaterThan(0);
        RuleFor(x => x.PickupAddress).NotEmpty().MaximumLength(255);
        RuleFor(x => x.DropoffAddress).NotEmpty().MaximumLength(255);
        RuleFor(x => x.ReceiverName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ReceiverPhone).ValidPhone();
        RuleFor(x => x.CouponCode).MaximumLength(50);
        RuleFor(x => x.ProhibitedGoodsCommitment).Equal(true).WithMessage("Confirm that the cargo has no prohibited goods.");
        RuleFor(x => x.PaymentMethod).IsInEnum();
        RuleFor(x => x.PayerRole).NotNull().When(x => x.PaymentMethod == PaymentMethod.Cash)
            .WithMessage("A cash order must say who pays: sender or receiver.");
        RuleFor(x => x.PayerRole).Null().When(x => x.PaymentMethod == PaymentMethod.PayOS)
            .WithMessage("A PayOS order is paid online by the customer: leave payerRole empty.");
        RuleFor(x => x.ExpectedTotalFee).GreaterThanOrEqualTo(0).Must(fee => fee % 1 == 0)
            .When(x => x.ExpectedTotalFee is not null).WithMessage("'Expected Total Fee' must be a whole VND amount, 0 or more.");

        RuleFor(x => x.Cargo).NotNull();
        When(x => x.Cargo is not null, () =>
        {
            RuleFor(x => x.Cargo.CargoType).NotEmpty().MaximumLength(MasterDataChoice.MaxCodeLength);
            RuleFor(x => x.Cargo.Quantity).GreaterThan(0);
            RuleFor(x => x.Cargo.WeightKg).GreaterThan(0).LessThanOrEqualTo(100_000);
            RuleFor(x => x.Cargo.LengthCm).InclusiveBetween(1, 2_000);
            RuleFor(x => x.Cargo.WidthCm).InclusiveBetween(1, 2_000);
            RuleFor(x => x.Cargo.HeightCm).InclusiveBetween(1, 2_000);
            RuleFor(x => x.Cargo.SpecialNotes).MaximumLength(500);
            RuleFor(x => x.Cargo.PhotoUrls)
                .NotNull()
                .Must(p => p.Count is >= 1 and <= BusinessRules.MaxCargoPhotos)
                .WithMessage($"Add between 1 and {BusinessRules.MaxCargoPhotos} cargo photos.");
            RuleForEach(x => x.Cargo.PhotoUrls)
                .Must(storage.IsOwnedUrl)
                .WithMessage("Cargo photos must be uploaded to ModMove first.")
                .When(x => x.Cargo.PhotoUrls is not null);
        });
    }
}
