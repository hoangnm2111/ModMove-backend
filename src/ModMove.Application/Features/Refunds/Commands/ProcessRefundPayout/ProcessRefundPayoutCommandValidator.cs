using FluentValidation;

namespace ModMove.Application.Features.Refunds.Commands.ProcessRefundPayout;

internal sealed class ProcessRefundPayoutCommandValidator : AbstractValidator<ProcessRefundPayoutCommand>
{
    public const int MaxBankReferenceCodeLength = 50;

    public ProcessRefundPayoutCommandValidator()
    {
        RuleFor(x => x.RefundId).GreaterThan(0);
        RuleFor(x => x.BankReferenceCode)
            .NotEmpty()
            .MaximumLength(MaxBankReferenceCodeLength);
    }
}

