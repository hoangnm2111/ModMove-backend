using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Notifications;
using ModMove.Domain.Constants;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.DriverOnboarding.Commands.RejectDriverApplication;

internal sealed class RejectDriverApplicationCommandHandler(
    IDriverProfileRepository driverProfiles,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TimeProvider clock,
    INotificationService? notifications = null) : ICommandHandler<RejectDriverApplicationCommand>
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

        var rejectedItems = request.RejectedItems?
            .Select(i => new RejectedApplicationItem(DriverApplicationItemKeys.Normalize(i.Key)!, i.Reason.Trim()))
            .ToList();
        var reasons = request.Reasons?
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .ToList() ?? [];

        // 1. Reject application (throws DomainException if not Pending or if no reasons/items)
        application.Reject(adminId, reasons, now, rejectedItems);

        // 2. Update driver profile status to Rejected
        driverProfile.Reject();

        // 3. Save changes
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // 4. Send push + real-time notification to driver (DECISIONS B17, 8.3)
        if (notifications is not null)
        {
            await notifications.NotifyApplicationRejectedAsync(driverProfile.UserId, application.Id, cancellationToken);
        }
    }
}
