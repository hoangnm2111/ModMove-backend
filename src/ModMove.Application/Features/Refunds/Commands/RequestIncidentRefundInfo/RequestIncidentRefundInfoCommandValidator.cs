using FluentValidation;

namespace ModMove.Application.Features.Refunds.Commands.RequestIncidentRefundInfo;

internal sealed class RequestIncidentRefundInfoCommandValidator : AbstractValidator<RequestIncidentRefundInfoCommand>
{
    public RequestIncidentRefundInfoCommandValidator()
    {
        RuleFor(x => x.IncidentId).GreaterThan(0);
        RuleFor(x => x.Note).NotEmpty().MaximumLength(CustomerRefundRules.MaxTextLength);
    }
}

