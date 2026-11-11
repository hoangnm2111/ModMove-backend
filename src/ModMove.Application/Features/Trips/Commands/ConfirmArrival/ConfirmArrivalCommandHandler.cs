using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Features.Incidents;
using ModMove.Application.Common.Notifications;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Trips.Commands.ConfirmArrival;

internal sealed class ConfirmArrivalCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IDriverProfileRepository driverProfiles,
    IShippingOrderRepository orders,
    IIncidentRepository incidents,
    INotificationService notifications,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<ConfirmArrivalCommand>
{
    public async Task Handle(ConfirmArrivalCommand request, CancellationToken cancellationToken)
    {
        var (driver, profile) = await DriverAccess.LoadApprovedDriverAsync(currentUser, users, driverProfiles, cancellationToken);
        var order = await orders.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), request.OrderId);

        order.EnsureDriver(profile.Id);
        await IncidentGuard.EnsureDriverHasNoOpenIncidentAsync(incidents, order.Id, driver.Id, cancellationToken); // 7.6
        order.ConfirmArrival(driver.Id, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notifications.PublishOrderStatusAsync(order, driver.Id, cancellationToken);
        await notifications.NotifyCustomerAsync(order, CustomerEvent.DriverArrived, cancellationToken);
    }
}

