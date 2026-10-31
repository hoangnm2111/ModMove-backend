using FluentValidation;

namespace ModMove.Application.Features.Refunds.Queries.GetMyRefundDetail;

internal sealed class GetMyRefundDetailQueryValidator : AbstractValidator<GetMyRefundDetailQuery>
{
    public GetMyRefundDetailQueryValidator() => RuleFor(x => x.RefundId).GreaterThan(0);
}
