using FluentValidation;

namespace ModMove.Application.Features.Refunds.Commands.RequestComplaintInfo;

internal sealed class RequestComplaintInfoCommandValidator : AbstractValidator<RequestComplaintInfoCommand>
{
    public RequestComplaintInfoCommandValidator()
    {
        RuleFor(x => x.ComplaintId).GreaterThan(0);
        RuleFor(x => x.Note).NotEmpty().MaximumLength(1000);
    }
}
