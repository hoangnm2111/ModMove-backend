using FluentValidation;

namespace ModMove.Application.Features.Orders.Queries.GetMyOrders;

internal sealed class GetMyOrdersQueryValidator : AbstractValidator<GetMyOrdersQuery>
{
    private static readonly DateOnly MinDate = new(2020, 1, 1);
    private static readonly DateOnly MaxDate = new(2100, 12, 31);

    public GetMyOrdersQueryValidator()
    {
        // Upper bound keeps (Page - 1) × PageSize inside int; 10,000 pages of 50 is far beyond any customer's history.
        RuleFor(x => x.Page).InclusiveBetween(1, 10_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
        RuleFor(x => x.View).IsInEnum();
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Status).IsInEnum();
        // Far outside any order, and keeps the Vietnam-day to UTC conversion away from DateTime.MinValue/MaxValue.
        RuleFor(x => x.FromDate).InclusiveBetween(MinDate, MaxDate);
        RuleFor(x => x.ToDate).InclusiveBetween(MinDate, MaxDate);
        RuleFor(x => x.ToDate).GreaterThanOrEqualTo(x => x.FromDate)
            .When(x => x.FromDate is not null && x.ToDate is not null)
            .WithMessage("'To Date' must not be before 'From Date'.");
        // Part of a phone number is enough to search, so only the characters are checked here, not the full format.
        RuleFor(x => x.ReceiverPhone).Matches("^[0-9]{1,15}$")
            .When(x => !string.IsNullOrWhiteSpace(x.ReceiverPhone))
            .WithMessage("'Receiver Phone' must be 1 to 15 digits.");
    }
}
