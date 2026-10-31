using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Ratings.Commands.RateTrip;

internal sealed class RateTripCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IShippingOrderRepository orders,
    IRatingRepository ratings,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<RateTripCommand, RateTripResponse>
{
    public async Task<RateTripResponse> Handle(RateTripCommand request, CancellationToken cancellationToken)
    {
        var customer = await currentUser.LoadUserAsync(users, cancellationToken);
        var order = await orders.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), request.OrderId);

        var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        // ORDER_NOT_YOURS, RATING_NOT_ALLOWED, RATING_WINDOW_CLOSED come from the entities.
        var rating = order.Rate(customer.Id, request.Stars, comment, clock.GetUtcNow());

        if (await ratings.ExistsForOrderAsync(order.Id, cancellationToken))
            throw new ConflictException("This trip has already been rated; a rating cannot be changed.");

        ratings.Add(rating);
        await unitOfWork.SaveChangesAsync(cancellationToken); // a concurrent second rating hits the unique index: 409
        return new RateTripResponse(rating.Id);
    }
}
