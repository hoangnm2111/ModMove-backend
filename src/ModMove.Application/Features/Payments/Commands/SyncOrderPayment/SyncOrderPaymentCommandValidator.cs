using FluentValidation;

namespace ModMove.Application.Features.Payments.Commands.SyncOrderPayment;

internal sealed class SyncOrderPaymentCommandValidator : AbstractValidator<SyncOrderPaymentCommand>
{
    public SyncOrderPaymentCommandValidator() => RuleFor(x => x.OrderId).GreaterThan(0);
}
