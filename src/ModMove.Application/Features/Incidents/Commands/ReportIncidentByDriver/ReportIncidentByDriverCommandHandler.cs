using System.Globalization;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Features.Incidents.Commands.ReportIncidentByCustomer;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Incidents.Commands.ReportIncidentByDriver;

internal sealed class ReportIncidentByDriverCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IDriverProfileRepository driverProfiles,
    IShippingOrderRepository orders,
    IRepository<InTransitIncident> incidents,
    INotificationService notifications,
    IFileStorage storage,
    IUnitOfWork unitOfWork) : ICommandHandler<ReportIncidentByDriverCommand, IncidentCreatedResponse>
{
    public async Task<IncidentCreatedResponse> Handle(
        ReportIncidentByDriverCommand request, CancellationToken cancellationToken)
    {
        var (driver, profile) = await DriverAccess.LoadApprovedDriverAsync(currentUser, users, driverProfiles, cancellationToken);
        var order = await orders.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), request.OrderId);

        // Fall back to the driver's last recorded GPS point so a mid-trip reassignment knows where the vehicle stopped (DECISIONS 7.2).
        var latitude = request.Latitude ?? profile.LastLatitude;
        var longitude = request.Longitude ?? profile.LastLongitude;
        if (latitude is null || longitude is null)
        {
            latitude = null;
            longitude = null;
        }

        var incident = order.ReportIncidentByDriver(
            profile.Id,
            driver.Id,
            request.IncidentType,
            request.Description.Trim(),
            request.PhotoUrls?.Select(storage.ToStoredUrl).ToList(),
            latitude,
            longitude);

        incidents.Add(incident);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var data = new Dictionary<string, string>
        {
            ["incidentId"] = incident.Id.ToString(CultureInfo.InvariantCulture),
            ["orderId"] = order.Id.ToString(CultureInfo.InvariantCulture),
        };

        await notifications.NotifyDispatchersAsync(
            new Notification("INCIDENT_REPORTED", $"Tài xế báo sự cố đơn {order.OrderCode}", incident.Description, data),
            cancellationToken);

        await notifications.NotifyAsync(
            order.CustomerId,
            new Notification(
                "INCIDENT_REPORTED",
                $"Sự cố trên đơn {order.OrderCode}",
                "Tài xế vừa báo cáo sự cố trên chuyến hàng. Điều phối viên đang kiểm tra và xử lý.",
                data),
            cancellationToken);

        return new IncidentCreatedResponse(incident.Id);
    }
}

