using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Features.Orders;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Incidents.Commands.CancelIncident;

internal sealed class CancelIncidentCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IRepository<InTransitIncident> incidents,
    IShippingOrderRepository orders,
    IRefundRepository refunds,
    IDriverProfileRepository driverProfiles,
    INotificationService notifications,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<CancelIncidentCommand>
{
    public async Task Handle(CancelIncidentCommand request, CancellationToken cancellationToken)
    {
        var dispatcher = await currentUser.LoadUserAsync(users, cancellationToken);
        var incident = await incidents.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new NotFoundException(nameof(InTransitIncident), request.IncidentId);

        IncidentHandlingRules.EnsureCanHandle(incident, dispatcher.Id);

        var order = await orders.GetByIdAsync(incident.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), incident.OrderId);

        var reason = request.Reason.Trim();
        incident.CancelIncident(reason, dispatcher.Id, clock.GetUtcNow());

        // GB-19 / DECISIONS B9: propose 0 VND on any open Requested incident refund so Admin finalizes rejection.
        var openRefund = await refunds.GetOpenForIncidentAsync(incident.Id, cancellationToken);
        if (openRefund is { RefundStatus: RefundStatus.Requested, SourceType: RefundSourceType.Incident })
            openRefund.Propose(0m, $"Incident cancelled by Dispatcher: {reason}", dispatcher.Id);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var driverUserId = await driverProfiles.DriverUserIdAsync(order, cancellationToken);
        await IncidentNotifications.NotifyIncidentCancelledAsync(notifications, order, driverUserId, incident, reason, cancellationToken);
    }
}

