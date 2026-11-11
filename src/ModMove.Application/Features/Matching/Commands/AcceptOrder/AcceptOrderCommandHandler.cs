using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Notifications;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.Application.Features.Matching.Commands.AcceptOrder;

internal sealed class AcceptOrderCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IDriverProfileRepository driverProfiles,
    IDriverWalletRepository wallets,
    IShippingOrderRepository orders,
    IPlatformSettings settings,
    INotificationService notifications,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<AcceptOrderCommand>
{
    public async Task Handle(AcceptOrderCommand request, CancellationToken cancellationToken)
    {
        var user = await currentUser.LoadUserAsync(users, cancellationToken);
        var order = await orders.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), request.OrderId);

        // GB-08: the first driver to accept wins; a driver arriving after the order was already assigned gets 409 CONFLICT (MSG32).
        if (order.DriverProfileId is not null)
            throw new ConflictException("This order has already been accepted by another driver.");

        if (order.OrderStatus == OrderStatus.Cancelled)
            throw new DomainException("This order was cancelled and can no longer be accepted.", "ORDER_CANCELLED");

        if (order.OrderStatus != OrderStatus.Pending)
            throw new DomainException($"Order is in {order.OrderStatus} status and cannot be accepted.");

        // GB-31, DECISIONS B10: a PayOS order cannot be matched or accepted before webhook payment confirmation.
        if (order.PaymentMethod == PaymentMethod.PayOS && order.PaymentStatus != PaymentStatus.Paid)
            throw new DomainException("An online-paid order cannot be accepted before payment is confirmed.");

        var now = clock.GetUtcNow();

        // GB-24, DECISIONS 3.2: orders past the 10-minute matching window cannot be accepted.
        if (order.IsMatchingExpired(now))
            throw new DomainException("The matching window for this order has expired.");

        var profile = await driverProfiles.GetByUserIdWithApplicationsAndVehicleAsync(user.Id, cancellationToken);
        if (profile is null || profile.ApprovalStatus != ApprovalStatus.Approved || profile.OnlineStatus != OnlineStatus.Online)
            throw NotEligible("Only an approved, online driver can accept orders.");

        if (profile.Vehicle is not { VehicleStatus: VehicleStatus.Active } vehicle || vehicle.VehicleTypeId != order.VehicleTypeId)
            throw NotEligible("Your active vehicle does not match the vehicle type required by this order.");

        if (!profile.HasFreshLocation(now)
            || profile.LastLatitude is not { } driverLat
            || profile.LastLongitude is not { } driverLon)
        {
            throw NotEligible("A GPS signal within the last 5 minutes is required to accept orders.");
        }

        var radiusKm = (double)await settings.GetDecimalAsync(SettingKeys.MatchingRadiusKm, cancellationToken);
        if (GeoDistance.HaversineKm(driverLat, driverLon, order.PickupLatitude, order.PickupLongitude) > radiusKm)
            throw NotEligible("The order pickup point is outside the matching radius.");

        // GB-09: at most one active order per driver at any time.
        if (await orders.GetActiveOrderIdForDriverAsync(profile.Id, cancellationToken) is not null)
            throw NotEligible("You already have an order in progress.");

        // GB-16: all orders require an Active wallet with DepositBalance >= REQUIRED_DEPOSIT; cash orders also require >= this order's commission.
        var wallet = await wallets.GetByDriverProfileIdAsync(profile.Id, cancellationToken);
        var requiredDeposit = await settings.GetDecimalAsync(SettingKeys.RequiredDeposit, cancellationToken);
        if (order.PaymentMethod == PaymentMethod.Cash)
        {
            var commissionRate = await settings.GetDecimalAsync(SettingKeys.CommissionRate, cancellationToken);
            if (wallet is null || !wallet.CanReceiveCod(requiredDeposit, order.CommissionFor(commissionRate)))
                throw NotEligible("Your deposit wallet balance is insufficient to accept this cash order.");
        }
        else if (wallet is null || !wallet.CanReceiveOrders(requiredDeposit))
        {
            throw NotEligible("Your deposit wallet balance is insufficient to accept orders.");
        }

        order.Accept(profile.Id, user.Id, now);
        orders.Update(order);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notifications.PublishOrderStatusAsync(order, user.Id, cancellationToken);
        await notifications.NotifyCustomerAsync(order, CustomerEvent.OrderAccepted, cancellationToken);
    }

    private static DomainException NotEligible(string message) => new(message, "DRIVER_NOT_ELIGIBLE");
}

