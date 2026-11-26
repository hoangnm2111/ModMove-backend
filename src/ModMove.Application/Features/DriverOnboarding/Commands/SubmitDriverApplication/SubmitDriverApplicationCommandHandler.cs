using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.Application.Features.DriverOnboarding.Commands.SubmitDriverApplication;

internal sealed class SubmitDriverApplicationCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IDriverProfileRepository driverProfiles,
    IRepository<VehicleType> vehicleTypes,
    ISqlConnectionFactory connectionFactory,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<SubmitDriverApplicationCommand, long>
{
    public async Task<long> Handle(SubmitDriverApplicationCommand request, CancellationToken cancellationToken)
    {
        var user = await DriverApplicationSubmissionHelper.LoadVerifiedDriverAsync(currentUser, users, cancellationToken);
        var vehicleType = await DriverApplicationSubmissionHelper.ValidateVehicleTypeAndLoadAsync(
            vehicleTypes, request.VehicleTypeId, request.MaxLoadWeightKg, cancellationToken);

        var plate = DriverApplicationSubmissionHelper.NormalizePlate(request.LicensePlate);

        var profile = await driverProfiles.GetByUserIdWithApplicationsAndVehicleAsync(user.Id, cancellationToken);

        // MSG07: a Pending application is read-only and an Approved one needs no new version. Checked before
        // the duplicate lookup so the driver learns the real reason.
        if (profile is not null && profile.ApprovalStatus != ApprovalStatus.Rejected)
            throw new DomainException(
                "A driver application is already pending review or approved.", "APPLICATION_NOT_EDITABLE");

        await DriverApplicationSubmissionHelper.EnsureNoDuplicatesAsync(
            connectionFactory, user.Id, plate, cancellationToken);

        var urls = request.DocumentUrls.Select(u => storage.ToStoredUrl(u.Trim())).ToList();
        var isNewProfile = profile is null;

        if (profile is null)
            profile = DriverProfile.Create(user.Id, urls[0], urls[1]);
        else
            profile.UpdateCredentials(urls[0], urls[1]);

        var application = DriverApplication.Create(
            profile.Id, profile.NextVersionNumber(), vehicleType.Id, plate,
            request.MaxLoadWeightKg, urls, clock.GetUtcNow());

        profile.Submit(application); // one Pending at a time; ProfileApprovalStatus becomes Pending

        if (isNewProfile)
            driverProfiles.Add(profile);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return application.Id;
    }
}

