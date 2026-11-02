using FluentValidation;

namespace ModMove.Application.Features.Wallets.Commands.ApproveWithdrawal;

internal sealed class ApproveWithdrawalCommandValidator : AbstractValidator<ApproveWithdrawalCommand>
{
    public ApproveWithdrawalCommandValidator()
    {
        RuleFor(x => x.TransactionId)
            .GreaterThan(0).WithMessage("Mã giao dịch rút tiền phải lớn hơn 0.");

        RuleFor(x => x.BankReferenceCode)
            .NotEmpty().WithMessage("Mã tham chiếu chuyển khoản ngân hàng là bắt buộc.")
            .MaximumLength(50).WithMessage("Mã tham chiếu chuyển khoản ngân hàng không được vượt quá 50 ký tự.");
    }
}

