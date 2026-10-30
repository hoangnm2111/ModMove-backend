using FluentValidation;

namespace ModMove.Application.Features.Orders.Commands.ConfirmReceipt;

internal sealed class ConfirmReceiptCommandValidator : AbstractValidator<ConfirmReceiptCommand>
{
    public ConfirmReceiptCommandValidator() => RuleFor(x => x.OrderId).GreaterThan(0);
}
