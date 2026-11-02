using FluentValidation;

namespace ModMove.Application.Features.Payments.Commands.CreateOrderPayment;

internal sealed class CreateOrderPaymentCommandValidator : AbstractValidator<CreateOrderPaymentCommand>
{
    public CreateOrderPaymentCommandValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
        RuleFor(x => x.Platform).IsInEnum(); // Web or App; the server maps it to a return URL
    }
}

