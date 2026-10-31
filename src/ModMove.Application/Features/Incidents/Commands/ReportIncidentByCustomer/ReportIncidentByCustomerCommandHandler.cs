using System.Globalization;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Incidents.Commands.ReportIncidentByCustomer;

internal sealed class ReportIncidentByCustomerCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IShippingOrderRepository orders,
    IRepository<InTransitIncident> incidents,
    INotificationService notifications,
    IUnitOfWork unitOfWork) : ICommandHandler<ReportIncidentByCustomerCommand, IncidentCreatedResponse>
{
    public async Task<IncidentCreatedResponse> Handle(ReportIncidentByCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await currentUser.LoadUserAsync(users, cancellationToken);
        var order = await orders.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), request.OrderId);

        // ORDER_NOT_YOURS and INCIDENT_NOT_ALLOWED come from the order.
        var incident = order.ReportIncidentByCustomer(
            customer.Id, request.IncidentType, request.Description.Trim(), request.PhotoUrls, request.Latitude, request.Longitude);
        incidents.Add(incident);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notifications.NotifyDispatchersAsync(new Notification(
            "INCIDENT_REPORTED", $"Khách báo sự cố đơn {order.OrderCode}", incident.Description,
            new Dictionary<string, string>
            {
                ["incidentId"] = incident.Id.ToString(CultureInfo.InvariantCulture),
                ["orderId"] = order.Id.ToString(CultureInfo.InvariantCulture),
            }), cancellationToken);
        return new IncidentCreatedResponse(incident.Id);
    }
}
