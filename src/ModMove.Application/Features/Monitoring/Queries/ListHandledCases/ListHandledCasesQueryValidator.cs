using FluentValidation;

namespace ModMove.Application.Features.Monitoring.Queries.ListHandledCases;

internal sealed class ListHandledCasesQueryValidator : AbstractValidator<ListHandledCasesQuery>
{
    public ListHandledCasesQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 10_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
        RuleFor(x => x.Type).IsInEnum().When(x => x.Type is not null);
    }
}

