using FluentValidation;
using ModMove.Application.Features.Auth;

namespace ModMove.Application.Features.Users.Commands.UpdateMyProfile;

internal sealed class UpdateMyProfileCommandValidator : AbstractValidator<UpdateMyProfileCommand>
{
    public UpdateMyProfileCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PhoneNumber).ValidPhone();
    }
}
