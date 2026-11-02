using FluentValidation;
using ModMove.Application.Common;

namespace ModMove.Application.Features.Orders.Commands.CancelOrder;

internal sealed class CancelOrderCommandValidator : AbstractValidator<CancelOrderCommand>
{
    public CancelOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
        RuleFor(x => x.ReasonCode).NotEmpty().MaximumLength(MasterDataChoice.MaxCodeLength);
        RuleFor(x => x.Reason).MaximumLength(500); // shipping_orders.cancellation_reason
    }
}
