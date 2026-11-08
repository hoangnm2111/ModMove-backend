using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Notifications;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.Application.Features.Trips.Commands.DeliverTrip;

internal sealed class DeliverTripCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IDriverProfileRepository driverProfiles,
    IShippingOrderRepository orders,
    IPaymentTransactionRepository payments,
    INotificationService notifications,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<DeliverTripCommand>
{
    public async Task Handle(DeliverTripCommand request, CancellationToken cancellationToken)
    {
        var (driver, profile) = await DriverAccess.LoadApprovedDriverAsync(currentUser, users, driverProfiles, cancellationToken);

        // Load with Proof so Deliver updates the existing proof_of_deliveries row instead of inserting a duplicate.
        var order = await orders.GetWithProofAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), request.OrderId);

        order.EnsureDriver(profile.Id);

        var now = clock.GetUtcNow();

        // UC-DRV-16 5b, UC-DRV-17: on a Cash order at drop-off (InTransit), cash must be collected before Delivered.
        if (order.OrderStatus == OrderStatus.InTransit
            && order.PaymentMethod == PaymentMethod.Cash
            && order.PaymentStatus != PaymentStatus.Paid)
        {
            if (order.TotalFee == 0)
            {
                order.MarkPaid(now);
            }
            else if (request.CashCollected)
            {
                var cashTx = order.CollectCash(profile.Id, now);
                payments.Add(cashTx);
            }
            else
            {
                throw new DomainException(
                    "Confirm cash collection before completing delivery of a cash order.",
                    "CASH_NOT_COLLECTED");
            }
        }

        order.Deliver(request.DeliveryPhotoUrl, driver.Id, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notifications.PublishOrderStatusAsync(order, driver.Id, cancellationToken);
        await notifications.NotifyCustomerAsync(order, CustomerEvent.OrderDelivered, cancellationToken);
    }
}

