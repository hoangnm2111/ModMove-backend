using FluentValidation;

namespace ModMove.Application.Features.Payments.Commands.CancelOrderPayment;

internal sealed class CancelOrderPaymentCommandValidator : AbstractValidator<CancelOrderPaymentCommand>
{
    public CancelOrderPaymentCommandValidator() => RuleFor(x => x.OrderId).GreaterThan(0);
}
