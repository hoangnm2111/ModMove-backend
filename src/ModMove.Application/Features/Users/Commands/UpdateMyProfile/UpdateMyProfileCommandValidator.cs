using FluentValidation;
using ModMove.Application.Features.Auth;

namespace ModMove.Application.Features.Users.Commands.UpdateMyProfile;

internal sealed class UpdateMyProfileCommandValidator : AbstractValidator<UpdateMyProfileCommand>
{
    public UpdateMyProfileCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PhoneNumber).ValidPhone();
        RuleFor(x => x.AvatarUrl)
            .MaximumLength(500)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
            .WithMessage("The avatar must be an https URL.")
            .When(x => x.AvatarUrl is not null);
    }
}
