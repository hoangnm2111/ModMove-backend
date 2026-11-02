using FluentValidation;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Wallets.Queries.GetAdminWithdrawals;

internal sealed class GetAdminWithdrawalsQueryValidator : AbstractValidator<GetAdminWithdrawalsQuery>
{
    public GetAdminWithdrawalsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Số trang phải lớn hơn hoặc bằng 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Kích thước trang phải từ 1 đến 100.");

        RuleFor(x => x.Status)
            .Must(s => s is null || Enum.TryParse<WalletTransactionStatus>(s, ignoreCase: true, out _))
            .WithMessage("Trạng thái giao dịch rút tiền không hợp lệ (Pending, Completed, Rejected).");
    }
}

