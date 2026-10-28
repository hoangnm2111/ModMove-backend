using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
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
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<SubmitDriverApplicationCommand, long>
{
    // Names the fields already used by another driver (GB-01 citizen ID / licence, GB-04 plate).
    // A plate is also "taken" while another driver's application for it is still waiting for review.
    private const string DuplicateSql = """
        SELECT 'CitizenIdNumber' FROM driver_profiles WHERE citizen_id_number = @CitizenIdNumber AND user_id <> @UserId
        UNION
        SELECT 'DriverLicenseNumber' FROM driver_profiles WHERE driver_license_number = @DriverLicenseNumber AND user_id <> @UserId
        UNION
        SELECT 'LicensePlate' FROM vehicles v JOIN driver_profiles dp ON dp.id = v.driver_profile_id
            WHERE v.license_plate = @LicensePlate AND dp.user_id <> @UserId
        UNION
        SELECT 'LicensePlate' FROM driver_applications da JOIN driver_profiles dp ON dp.id = da.driver_profile_id
            WHERE da.license_plate = @LicensePlate AND da.application_status = 'Pending' AND dp.user_id <> @UserId
        """;

    public async Task<long> Handle(SubmitDriverApplicationCommand request, CancellationToken cancellationToken)
    {
        var user = await currentUser.LoadUserAsync(users, cancellationToken);
        if (user.Role != UserRole.Driver)
            throw new ForbiddenAccessException("Only drivers can submit a driver application.");
        if (!user.EmailVerified)
            throw new ForbiddenAccessException("Verify your email before submitting an application.", "EMAIL_NOT_VERIFIED");

        var vehicleType = await vehicleTypes.GetByIdAsync(request.VehicleTypeId, cancellationToken)
                          ?? throw new NotFoundException(nameof(VehicleType), request.VehicleTypeId);
        if (!vehicleType.IsActive)
            throw new DomainException("This vehicle type is not available.");
        if (request.MaxLoadWeightKg > vehicleType.MaxLoadWeightKg)
            throw new DomainException($"The maximum load of this vehicle type is {vehicleType.MaxLoadWeightKg:0.##} kg.");

        var citizenId = request.CitizenIdNumber.Trim();
        var licenseNumber = request.DriverLicenseNumber.Trim().ToUpperInvariant();
        var plate = request.LicensePlate.Trim().ToUpperInvariant();

        var profile = await driverProfiles.GetByUserIdWithApplicationsAndVehicleAsync(user.Id, cancellationToken);

        // MSG07: a Pending application is read-only and an Approved one needs no new version. Checked before
        // the duplicate lookup so the driver learns the real reason.
        if (profile is not null && profile.ApprovalStatus != ApprovalStatus.Rejected)
            throw new DomainException(
                "A driver application is already pending review or approved.", "APPLICATION_NOT_EDITABLE");

        using (var connection = connectionFactory.CreateConnection())
        {
            var duplicates = (await connection.QueryAsync<string>(new CommandDefinition(
                DuplicateSql,
                new { CitizenIdNumber = citizenId, DriverLicenseNumber = licenseNumber, LicensePlate = plate, UserId = user.Id },
                cancellationToken: cancellationToken))).ToList();

            if (duplicates.Count > 0)
                throw new ConflictException($"Already used by another driver: {string.Join(", ", duplicates)}.");
        }

        var urls = request.DocumentUrls.Select(u => u.Trim()).ToList();
        var isNewProfile = profile is null;

        if (profile is null)
            profile = DriverProfile.Create(user.Id, citizenId, licenseNumber, urls[0], urls[1]);
        else
            profile.UpdateCredentials(citizenId, licenseNumber, urls[0], urls[1]);

        var application = DriverApplication.Create(
            profile.Id, profile.NextVersionNumber(), vehicleType.Id, citizenId, licenseNumber, plate,
            request.MaxLoadWeightKg, urls, clock.GetUtcNow());

        profile.Submit(application); // one Pending at a time; ProfileApprovalStatus becomes Pending

        if (isNewProfile)
            driverProfiles.Add(profile);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return application.Id;
    }
}

