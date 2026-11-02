using FluentValidation;

namespace ModMove.Application.Features.Refunds.Queries.GetAdminRefunds;

internal sealed class GetAdminRefundsQueryValidator : AbstractValidator<GetAdminRefundsQuery>
{
    public GetAdminRefundsQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 10_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
        RuleFor(x => x.SourceType).IsInEnum().When(x => x.SourceType.HasValue);
    }
}

