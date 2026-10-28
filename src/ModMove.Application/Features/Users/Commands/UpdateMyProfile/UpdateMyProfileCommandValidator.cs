using FluentValidation;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Auth;

namespace ModMove.Application.Features.Users.Commands.UpdateMyProfile;

internal sealed class UpdateMyProfileCommandValidator : AbstractValidator<UpdateMyProfileCommand>
{
    public UpdateMyProfileCommandValidator(IFileStorage storage)
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PhoneNumber).ValidPhone();
        // Security rule: only images uploaded to the team's Cloudinary (via api/customer/uploads).
        RuleFor(x => x.AvatarUrl!)
            .MaximumLength(500)
            .Must(storage.IsOwnedUrl)
            .WithMessage("The avatar must be an image uploaded to ModMove.")
            .When(x => x.AvatarUrl is not null);
    }
}
