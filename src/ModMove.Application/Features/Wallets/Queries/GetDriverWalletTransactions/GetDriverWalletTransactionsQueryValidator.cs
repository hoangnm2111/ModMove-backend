using FluentValidation;

namespace ModMove.Application.Features.Wallets.Queries.GetDriverWalletTransactions;

internal sealed class GetDriverWalletTransactionsQueryValidator : AbstractValidator<GetDriverWalletTransactionsQuery>
{
    public GetDriverWalletTransactionsQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 10_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
    }
}

