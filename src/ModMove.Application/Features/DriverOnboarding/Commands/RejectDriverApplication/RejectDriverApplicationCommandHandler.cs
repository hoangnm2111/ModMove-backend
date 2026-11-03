using Microsoft.Extensions.Logging;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Notifications;

namespace ModMove.Application.Features.DriverOnboarding.Commands.RejectDriverApplication;

internal sealed class RejectDriverApplicationCommandHandler(
    IDriverProfileRepository driverProfiles,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TimeProvider clock,
    IPushNotificationService? pushNotifications = null,
    IRealtimeNotifier? realtime = null,
    ILogger<RejectDriverApplicationCommandHandler>? logger = null) : ICommandHandler<RejectDriverApplicationCommand>
{
    public async Task Handle(RejectDriverApplicationCommand request, CancellationToken cancellationToken)
    {
        var adminId = currentUser.UserId ?? throw new UnauthorizedException();
        var now = clock.GetUtcNow();

        var driverProfile = await driverProfiles.GetByApplicationIdWithApplicationsAndVehicleAsync(request.ApplicationId, cancellationToken);
        if (driverProfile is null)
            throw new NotFoundException("DriverApplication", request.ApplicationId);

        var application = driverProfile.Applications.FirstOrDefault(a => a.Id == request.ApplicationId);
        if (application is null)
            throw new NotFoundException("DriverApplication", request.ApplicationId);

        // 1. Reject application (throws DomainException if not Pending or if reasons is empty)
        application.Reject(adminId, request.Reasons, now);

        // 2. Update driver profile status to Rejected
        driverProfile.Reject();

        // 3. Save changes
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // 4. Send push + real-time notification to driver (DECISIONS B17, 8.3)
        var (type, title, body) = DriverNotifications.GetTemplate(DriverEvent.ApplicationRejected);
        var data = new Dictionary<string, string>
        {
            ["type"] = type,
            ["applicationId"] = application.Id.ToString(),
        };

        if (realtime is not null)
        {
            try
            {
                await realtime.SendToUserAsync(
                    driverProfile.UserId,
                    "NotificationReceived",
                    new Notification(type, title, body, data),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to send SignalR notification for rejected driver application {ApplicationId}", application.Id);
            }
        }

        if (pushNotifications is not null)
        {
            try
            {
                var user = await users.GetByIdAsync(driverProfile.UserId, cancellationToken);
                if (!string.IsNullOrWhiteSpace(user?.FcmToken))
                {
                    await pushNotifications.SendAsync(user.FcmToken, title, body, data, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to send push notification for rejected driver application {ApplicationId}", application.Id);
            }
        }
    }
}
