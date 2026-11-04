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
    IIncidentRepository incidents,
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

        var openRefunds = await refunds.GetOpenForOrderAsync(order.Id, cancellationToken);
        var zeroedOutSum = 0m;
        foreach (var r in openRefunds)
        {
            if (r.RefundStatus == RefundStatus.Requested && r.SourceType == RefundSourceType.Incident)
            {
                zeroedOutSum += r.ProposedAmount ?? r.RequestedAmount;
                r.Propose(
                    0m,
                    request.Refund
                        ? $"Superseded by order cancellation #{incident.Id}: {reason}"
                        : $"Order cancelled without refund: {reason}",
                    dispatcher.Id);
            }
        }

        var totalClaimedFromDb = await refunds.GetClaimedTotalForOrderAsync(order.Id, cancellationToken);
        var actualAlreadyClaimed = Math.Max(0m, totalClaimedFromDb - zeroedOutSum);
        var remaining = Math.Max(0m, order.TotalFee - actualAlreadyClaimed);

        if (request.Refund && remaining > 0m)
        {
            order.EnsureRefundFits(remaining, actualAlreadyClaimed);
            refunds.Add(Refund.Request(order.Id, incident.Id, RefundSourceType.Cancellation, reason, null, remaining));
        }

        var now = clock.GetUtcNow();
        order.Cancel(note, ChangedByRole.Dispatcher, dispatcher.Id, now);
        incident.Resolve(IncidentDecision.CancelOrder, note, dispatcher.Id, now);

        var otherOpenIncidents = await incidents.GetOpenForOrderAsync(order.Id, incident.Id, cancellationToken);
        foreach (var other in otherOpenIncidents)
        {
            other.CancelIncident($"Đơn đã hủy theo sự cố #{incident.Id}", dispatcher.Id, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var driverUserId = await driverProfiles.DriverUserIdAsync(order, cancellationToken);
        var customerDetail = request.Refund
            ? (remaining == order.TotalFee
                ? $"Đơn đã bị hủy và chuyển duyệt hoàn 100% tiền cước. {note}"
                : remaining > 0m
                    ? $"Đơn đã bị hủy và chuyển duyệt hoàn phần cước còn lại ({remaining:N0} đ). {note}"
                    : $"Đơn đã bị hủy (toàn bộ tiền cước đã được duyệt hoàn trước đó). {note}")
            : $"Đơn đã bị hủy. {note}";
        await IncidentNotifications.NotifyOrderCancelledAsync(
            notifications, order, driverUserId, incident, customerDetail, note, cancellationToken);
    }
}

