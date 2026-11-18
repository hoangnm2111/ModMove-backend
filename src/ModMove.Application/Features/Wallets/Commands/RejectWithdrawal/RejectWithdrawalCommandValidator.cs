using FluentValidation;

namespace ModMove.Application.Features.Wallets.Commands.RejectWithdrawal;

internal sealed class RejectWithdrawalCommandValidator : AbstractValidator<RejectWithdrawalCommand>
{
    public RejectWithdrawalCommandValidator()
    {
        RuleFor(x => x.TransactionId)
            .GreaterThan(0).WithMessage("Mã giao dịch rút tiền phải lớn hơn 0.");

        RuleFor(x => x.Reason)
            .MaximumLength(1000).WithMessage("Lý do từ chối không được vượt quá 1000 ký tự.")
            .When(x => x.Reason is not null);

        RuleFor(x => x.Note)
            .MaximumLength(1000).WithMessage("Ghi chú từ chối không được vượt quá 1000 ký tự.")
            .When(x => x.Note is not null);

        RuleFor(x => x.ReasonCode)
            .IsInEnum().WithMessage("Mã lý do từ chối không hợp lệ (WrongBankAccount hoặc Other).")
            .When(x => x.ReasonCode is not null);
    }
}

