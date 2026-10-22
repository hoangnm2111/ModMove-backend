using FluentValidation;

namespace ModMove.Application.Features.Users.Commands.SubmitSupportRequest;

/// <summary>Limits match support_requests (100/100/1000), so long input is a 400, not a database error.</summary>
internal sealed class SubmitSupportRequestCommandValidator : AbstractValidator<SubmitSupportRequestCommand>
{
    public SubmitSupportRequestCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Contact).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Content).NotEmpty().MaximumLength(1000);
    }
}
