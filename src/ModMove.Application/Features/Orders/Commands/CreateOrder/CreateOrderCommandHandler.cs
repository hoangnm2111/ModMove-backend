using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.Application.Features.Orders.Commands.CreateOrder;

internal sealed class CreateOrderCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IShippingOrderRepository orders,
    OrderPricing pricing,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<CreateOrderCommand, CreateOrderResponse>
{
    public async Task<CreateOrderResponse> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var customer = await currentUser.LoadUserAsync(users, cancellationToken);

        if (request.PaymentMethod != PaymentMethod.Cash)
            throw new DomainException("Only cash payment is available for now.", "PAYMENT_METHOD_NOT_SUPPORTED");

        var cargo = request.Cargo;
        var trip = await pricing.PriceAsync(new PricingInput(
            request.VehicleTypeId, request.PickupAddress, request.DropoffAddress,
            cargo.WeightKg, cargo.LengthCm, cargo.WidthCm, cargo.HeightCm,
            request.HasLoadingService, request.CouponCode), cancellationToken);

        var now = clock.GetUtcNow();
        var (dayStart, dayEnd) = OrderCode.VietnamDay(now);
        // Two orders racing for the same number: the unique index on order_code makes the second save a 409.
        var code = OrderCode.For(now, await orders.CountCreatedBetweenAsync(dayStart, dayEnd, cancellationToken) + 1);

        var order = ShippingOrder.Create(
            code, customer.Id, trip.VehicleType.Id, trip.Coupon?.Id,
            request.PickupAddress.Trim(), trip.Pickup.Latitude, trip.Pickup.Longitude,
            request.DropoffAddress.Trim(), trip.Dropoff.Latitude, trip.Dropoff.Longitude,
            request.ReceiverName.Trim(), request.ReceiverPhone.Trim(),
            trip.Route.DistanceKm, trip.Route.DurationMinutes, request.HasLoadingService,
            trip.Fare.DistanceFee, trip.Fare.ServiceFee, trip.Fare.DiscountAmount,
            request.PaymentMethod, request.PayerRole,
            CargoDetail.Create(
                cargo.CargoType.Trim(), cargo.Quantity, cargo.WeightKg, cargo.LengthCm, cargo.WidthCm, cargo.HeightCm,
                cargo.PhotoUrls, cargo.SpecialNotes?.Trim()),
            warrantyAcceptedAt: now);

        trip.Coupon?.MarkUsed(); // B11: counted with the order, in the same save
        orders.Add(order);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateOrderResponse(order.Id, order.OrderCode, order.TotalFee);
    }
}
