using FluentValidation;

namespace ModMove.Application.Features.Refunds.Queries.GetMyRefunds;

internal sealed class GetMyRefundsQueryValidator : AbstractValidator<GetMyRefundsQuery>
{
    public GetMyRefundsQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 10_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}
