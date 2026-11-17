using System.Globalization;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Features.Incidents;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Trips.Commands.NotifyArrivalAtDropoff;

internal sealed class NotifyArrivalAtDropoffCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IDriverProfileRepository driverProfiles,
    IShippingOrderRepository orders,
    IIncidentRepository incidents,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    INotificationService notifications) : ICommandHandler<NotifyArrivalAtDropoffCommand>
{
    public async Task Handle(NotifyArrivalAtDropoffCommand request, CancellationToken cancellationToken)
    {
        var (driver, profile) = await DriverAccess.LoadApprovedDriverAsync(currentUser, users, driverProfiles, cancellationToken);
        var order = await orders.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), request.OrderId);

        order.EnsureDriver(profile.Id);
        await IncidentGuard.EnsureDriverHasNoOpenIncidentAsync(incidents, order.Id, driver.Id, cancellationToken); // 7.6

        order.MarkArrivedAtDropoff(profile.Id, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notifications.NotifyAsync(
            order.CustomerId,
            new Notification(
                "DRIVER_ARRIVED_DROPOFF",
                "Tài xế đã đến điểm giao",
                $"Tài xế đã đến điểm giao hàng của đơn {order.OrderCode}.",
                new Dictionary<string, string>
                {
                    ["orderId"] = order.Id.ToString(CultureInfo.InvariantCulture),
                }),
            cancellationToken);
    }
}

