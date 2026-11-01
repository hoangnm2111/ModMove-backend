using FluentValidation;

namespace ModMove.Application.Features.Monitoring.Queries.GetDispatcherOrderById;

internal sealed class GetDispatcherOrderByIdQueryValidator : AbstractValidator<GetDispatcherOrderByIdQuery>
{
    public GetDispatcherOrderByIdQueryValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

