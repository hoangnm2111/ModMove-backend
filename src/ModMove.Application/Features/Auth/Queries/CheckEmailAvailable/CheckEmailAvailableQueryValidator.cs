using FluentValidation;

namespace ModMove.Application.Features.Auth.Queries.CheckEmailAvailable;

internal sealed class CheckEmailAvailableQueryValidator : AbstractValidator<CheckEmailAvailableQuery>
{
    public CheckEmailAvailableQueryValidator()
    {
        RuleFor(x => x.Email).ValidEmail();
    }
}
