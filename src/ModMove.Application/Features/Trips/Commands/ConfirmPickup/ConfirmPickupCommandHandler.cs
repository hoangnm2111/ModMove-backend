using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Notifications;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Trips.Commands.ConfirmPickup;

internal sealed class ConfirmPickupCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IDriverProfileRepository driverProfiles,
    IShippingOrderRepository orders,
    INotificationService notifications,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<ConfirmPickupCommand>
{
    public async Task Handle(ConfirmPickupCommand request, CancellationToken cancellationToken)
    {
        var (driver, profile) = await DriverAccess.LoadApprovedDriverAsync(currentUser, users, driverProfiles, cancellationToken);

        // Load with Proof so resubmitting after customer rejection updates the existing proof_of_deliveries row (DECISIONS 6.3).
        var order = await orders.GetWithProofAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), request.OrderId);

        order.EnsureDriver(profile.Id);
        order.ConfirmPickup(request.PhotoUrls, driver.Id, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notifications.PublishOrderStatusAsync(order, driver.Id, cancellationToken);
        await notifications.NotifyCustomerAsync(order, CustomerEvent.PickupPhotosToReview, cancellationToken);
    }
}

