using FluentValidation;

namespace ModMove.Application.Features.Refunds.Queries.ListComplaints;

internal sealed class ListComplaintsQueryValidator : AbstractValidator<ListComplaintsQuery>
{
    public ListComplaintsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Stage).IsInEnum();
        RuleFor(x => x.Search).MaximumLength(20);
    }
}
