using FluentValidation;

namespace ModMove.Application.Features.Wallets.Queries.GetDriverWithdrawals;

internal sealed class GetDriverWithdrawalsQueryValidator : AbstractValidator<GetDriverWithdrawalsQuery>
{
    public GetDriverWithdrawalsQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 10_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
        RuleFor(x => x.Status).IsInEnum();
    }
}

