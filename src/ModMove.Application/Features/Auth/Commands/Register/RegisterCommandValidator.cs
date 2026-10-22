using FluentValidation;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Auth.Commands.Register;

internal sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PhoneNumber).ValidPhone();
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.Password).ValidPassword();

        // Dispatchers and Administrators are created by an Administrator, never self-registered (B7).
        RuleFor(x => x.Role).Must(r => r is UserRole.Customer or UserRole.Driver)
            .WithMessage("Only customers and drivers can register.");
    }
}
