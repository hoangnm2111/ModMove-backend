using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Features.Orders;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.Application.Features.Incidents.Commands.CancelOrderByIncident;

internal sealed class CancelOrderByIncidentCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IRepository<InTransitIncident> incidents,
    IShippingOrderRepository orders,
    IRefundRepository refunds,
    IDriverProfileRepository driverProfiles,
    INotificationService notifications,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<CancelOrderByIncidentCommand>
{
    public async Task Handle(CancelOrderByIncidentCommand request, CancellationToken cancellationToken)
    {
        var dispatcher = await currentUser.LoadUserAsync(users, cancellationToken);
        var incident = await incidents.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new NotFoundException(nameof(InTransitIncident), request.IncidentId);

        IncidentHandlingRules.EnsureCanHandle(incident, dispatcher.Id);

        var order = await orders.GetByIdAsync(incident.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), incident.OrderId);

        if (order.OrderStatus is not (OrderStatus.Accepted or OrderStatus.Arrived or OrderStatus.PickedUp or OrderStatus.InTransit))
            throw new DomainException("Only an active order with a driver can be cancelled due to an incident.", "ORDER_NOT_CANCELLABLE");

        var cargoOnVehicle = order.OrderStatus is OrderStatus.PickedUp or OrderStatus.InTransit;
        if (cargoOnVehicle && request.CargoDisposition is null)
        {
            throw new DomainException(
                "Cargo is already on the vehicle; specify whether the driver returns it to pickup or keeps it.",
                "CARGO_DISPOSITION_REQUIRED");
        }

        if (!cargoOnVehicle && request.CargoDisposition is not null)
            throw new DomainException("Cargo has not been picked up yet; cargo disposition does not apply.");

        if (request.Refund && (order.PaymentMethod != PaymentMethod.PayOS || order.PaymentStatus != PaymentStatus.Paid))
            throw new DomainException("A cancellation refund is only allowed for a paid PayOS order.", "REFUND_NOT_ALLOWED");

        var reason = request.Reason.Trim();
        var note = request.CargoDisposition is { } disposition
            ? $"{reason} [Cargo: {disposition}]"
            : reason;

        var openIncidentRefund = await refunds.GetOpenForIncidentAsync(incident.Id, cancellationToken);
        if (request.Refund)
        {
            if (openIncidentRefund is { RefundStatus: RefundStatus.Approved })
                throw new DomainException("An incident refund on this order has already been approved.");

            if (openIncidentRefund is { RefundStatus: RefundStatus.Requested, SourceType: RefundSourceType.Incident })
                openIncidentRefund.Propose(0m, $"Superseded by full cancellation refund: {reason}", dispatcher.Id);

            refunds.Add(Refund.Request(order.Id, incident.Id, RefundSourceType.Cancellation, reason, null, order.TotalFee));
        }
        else if (openIncidentRefund is { RefundStatus: RefundStatus.Requested, SourceType: RefundSourceType.Incident })
        {
            openIncidentRefund.Propose(0m, $"Order cancelled without refund: {reason}", dispatcher.Id);
        }

        var now = clock.GetUtcNow();
        order.Cancel(note, ChangedByRole.Dispatcher, dispatcher.Id, now);
        incident.Resolve(IncidentDecision.CancelOrder, note, dispatcher.Id, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var driverUserId = await driverProfiles.DriverUserIdAsync(order, cancellationToken);
        var customerDetail = request.Refund
            ? $"Đơn đã bị hủy và chuyển duyệt hoàn 100% tiền cước. {note}"
            : $"Đơn đã bị hủy. {note}";
        await IncidentNotifications.NotifyOrderCancelledAsync(
            notifications, order, driverUserId, incident, customerDetail, note, cancellationToken);
    }
}

