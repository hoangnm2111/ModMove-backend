using FluentValidation;

namespace ModMove.Application.Features.Refunds.Queries.GetComplaintById;

internal sealed class GetComplaintByIdQueryValidator : AbstractValidator<GetComplaintByIdQuery>
{
    public GetComplaintByIdQueryValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
