using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Notifications;
using ModMove.Application.Features.MasterData.Queries.GetPublicContent;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Orders.Commands.CreateOrder;

internal sealed class CreateOrderCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IShippingOrderRepository orders,
    OrderPricing pricing,
    IMasterDataRepository masterData,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    INotificationService? notifications = null,
    IDriverProfileRepository? driverProfiles = null,
    IDriverWalletRepository? wallets = null,
    IPlatformSettings? settings = null) : ICommandHandler<CreateOrderCommand, CreateOrderResponse>
{
    public async Task<CreateOrderResponse> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var customer = await currentUser.LoadUserAsync(users, cancellationToken);

        var cargo = request.Cargo;
        var cargoType = await masterData.RequireActiveAsync(
            PublicContentCategories.CargoType, cargo.CargoType, "Cargo.CargoType", cancellationToken);

        var trip = await pricing.PriceAsync(new PricingInput(
            request.VehicleTypeId, request.PickupAddress, request.DropoffAddress,
            cargo.WeightKg, cargo.LengthCm, cargo.WidthCm, cargo.HeightCm,
            request.HasLoadingService, request.CouponCode), cancellationToken);
        trip.Fare.EnsureMatches(request.ExpectedTotalFee); // before anything is saved or the coupon is counted

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
                cargoType.Code, cargo.Quantity, cargo.WeightKg, cargo.LengthCm, cargo.WidthCm, cargo.HeightCm,
                cargo.PhotoUrls.Select(storage.ToStoredUrl).ToList(), cargo.SpecialNotes?.Trim()),
            warrantyAcceptedAt: now,
            senderName: string.IsNullOrWhiteSpace(request.SenderName) ? customer.FullName : request.SenderName,
            senderPhone: string.IsNullOrWhiteSpace(request.SenderPhone) ? customer.PhoneNumber : request.SenderPhone,
            pickupNote: request.PickupNote,
            dropoffNote: request.DropoffNote);

        trip.Coupon?.MarkUsed(); // B11: counted with the order, in the same save
        orders.Add(order);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // DECISIONS 6.4, 8.3: Cash orders enter the matching pool immediately on creation;
        // PayOS orders wait for ProcessPayOsWebhookCommandHandler once PaidAt is set.
        if (order.PaymentMethod == PaymentMethod.Cash
            && notifications is not null
            && driverProfiles is not null
            && wallets is not null
            && settings is not null)
        {
            await notifications.NotifyNearbyDriversOfNewOrderAsync(
                order, driverProfiles, wallets, orders, settings, now, cancellationToken);
        }

        return new CreateOrderResponse(order.Id, order.OrderCode, order.TotalFee);
    }
}
