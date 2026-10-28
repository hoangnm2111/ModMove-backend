using FluentValidation;
using ModMove.Application.Features.Auth;

namespace ModMove.Application.Features.Users.Commands.CreateDispatcher;

internal sealed class CreateDispatcherCommandValidator : AbstractValidator<CreateDispatcherCommand>
{
    public CreateDispatcherCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PhoneNumber).ValidPhone();
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.Password).ValidPassword();
    }
}

