using FluentValidation;

namespace ModMove.Application.Features.Payments.Commands.ProcessPayOsWebhook;

internal sealed class ProcessPayOsWebhookCommandValidator : AbstractValidator<ProcessPayOsWebhookCommand>
{
    public ProcessPayOsWebhookCommandValidator()
    {
        RuleFor(x => x.RawBody).NotEmpty();
    }
}

