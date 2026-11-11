using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Notifications;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.DriverOnboarding.Commands.ApproveDriverApplication;

internal sealed class ApproveDriverApplicationCommandHandler(
    IDriverProfileRepository driverProfiles,
    IDriverWalletRepository driverWallets,
    IRepository<VehicleType> vehicleTypes,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TimeProvider clock,
    INotificationService? notifications = null) : ICommandHandler<ApproveDriverApplicationCommand>
{
    public async Task Handle(ApproveDriverApplicationCommand request, CancellationToken cancellationToken)
    {
        var adminId = currentUser.UserId ?? throw new UnauthorizedException();
        var now = clock.GetUtcNow();

        var driverProfile = await driverProfiles.GetByApplicationIdWithApplicationsAndVehicleAsync(request.ApplicationId, cancellationToken);
        if (driverProfile is null)
            throw new NotFoundException("DriverApplication", request.ApplicationId);

        var application = driverProfile.Applications.FirstOrDefault(a => a.Id == request.ApplicationId);
        if (application is null)
            throw new NotFoundException("DriverApplication", request.ApplicationId);

        var vehicleType = await vehicleTypes.GetByIdAsync(application.VehicleTypeId, cancellationToken);
        if (vehicleType is null)
            throw new NotFoundException("VehicleType", application.VehicleTypeId);

        // 1. Approve application (throws DomainException if not Pending)
        application.Approve(adminId, now);

        // 2. Create/update Vehicle from application data
        var docs = application.DocumentUrls;
        var regCert = docs.ElementAtOrDefault(2) ?? "";
        var inspCert = docs.ElementAtOrDefault(3) ?? "";
        var vehPhoto = docs.ElementAtOrDefault(4) ?? "";
        var dimensions = $"{vehicleType.CargoBedLengthCm}x{vehicleType.CargoBedWidthCm}x{vehicleType.CargoBedHeightCm}";

        var vehicle = Vehicle.Create(
            driverProfile.Id,
            vehicleType.Id,
            application.LicensePlate,
            application.MaxLoadWeightKg,
            dimensions,
            regCert,
            inspCert,
            vehPhoto);

        driverProfile.Approve(vehicle);

        // 3. Automatically provision DriverWallet if first time approved (GB-16, TEAM-PLAN line 74)
        var wallet = await driverWallets.GetByDriverProfileIdAsync(driverProfile.Id, cancellationToken);
        if (wallet is null)
        {
            wallet = DriverWallet.Create(driverProfile.Id);
            driverWallets.Add(wallet);
        }

        // 4. Save changes
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // 5. Send push + real-time notification to driver (DECISIONS B17, 8.3)
        if (notifications is not null)
        {
            await notifications.NotifyApplicationApprovedAsync(driverProfile.UserId, application.Id, cancellationToken);
        }
    }
}
