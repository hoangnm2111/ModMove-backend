using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Features.DriverOnboarding.Commands.SubmitDriverApplication;
using ModMove.Domain.Constants;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.Application.Features.DriverOnboarding.Commands.ResubmitDriverApplication;

internal sealed class ResubmitDriverApplicationCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IDriverProfileRepository driverProfiles,
    IRepository<VehicleType> vehicleTypes,
    ISqlConnectionFactory connectionFactory,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<ResubmitDriverApplicationCommand, long>
{
    public async Task<long> Handle(ResubmitDriverApplicationCommand request, CancellationToken cancellationToken)
    {
        var user = await DriverApplicationSubmissionHelper.LoadVerifiedDriverAsync(currentUser, users, cancellationToken);

        var profile = await driverProfiles.GetByUserIdWithApplicationsAndVehicleAsync(user.Id, cancellationToken)
            ?? throw new NotFoundException("DriverProfile", user.Id);

        if (profile.ApprovalStatus != ApprovalStatus.Rejected)
            throw new DomainException(
                "Only a rejected driver application can be resubmitted.", "APPLICATION_NOT_EDITABLE");

        var latest = profile.Applications
            .OrderByDescending(a => a.VersionNumber)
            .FirstOrDefault()
            ?? throw new NotFoundException("DriverApplication", profile.Id);

        var rejectedKeys = latest.RejectedItems?
            .Select(i => DriverApplicationItemKeys.Normalize(i.Key))
            .Where(k => k is not null)
            .Select(k => k!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var submittedDocKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var urls = latest.DocumentUrls.ToList();
        while (urls.Count < SubmitDriverApplicationCommandValidator.RequiredDocumentCount)
            urls.Add(string.Empty);

        foreach (var item in request.Items ?? [])
        {
            var normalizedKey = DriverApplicationItemKeys.Normalize(item.Key);
            if (normalizedKey is null)
                continue;

            // Keep previously accepted items untouched when structured rejection items exist.
            if (rejectedKeys is { Count: > 0 } && !rejectedKeys.Contains(normalizedKey))
                continue;

            submittedDocKeys.Add(normalizedKey);
            if (DriverApplicationItemKeys.DocumentIndexByKey.TryGetValue(normalizedKey, out var index))
                urls[index] = item.Url.Trim();
        }

        if (rejectedKeys is { Count: > 0 })
        {
            var missing = rejectedKeys
                .Where(k => DriverApplicationItemKeys.DocumentIndexByKey.ContainsKey(k) && !submittedDocKeys.Contains(k))
                .ToList();

            if (rejectedKeys.Contains(DriverApplicationItemKeys.VehicleInfo) && request.Vehicle is null)
                missing.Add(DriverApplicationItemKeys.VehicleInfo);

            if (missing.Count > 0)
            {
                throw new DomainException(
                    $"Please resubmit all rejected items: {string.Join(", ", missing)}.",
                    "RESUBMIT_MISSING_REJECTED_ITEMS");
            }
        }

        if (urls.Count != SubmitDriverApplicationCommandValidator.RequiredDocumentCount
            || urls.Any(string.IsNullOrWhiteSpace))
        {
            throw new DomainException(
                "All 7 required documents must be present after merging resubmitted items.",
                "MISSING_REQUIRED_DOCUMENTS");
        }

        var canUpdateVehicle = rejectedKeys is null || rejectedKeys.Count == 0 || rejectedKeys.Contains(DriverApplicationItemKeys.VehicleInfo);
        var vehicleTypeId = canUpdateVehicle && request.Vehicle?.VehicleTypeId is { } newTypeId ? newTypeId : latest.VehicleTypeId;
        var licensePlateInput = canUpdateVehicle && request.Vehicle?.LicensePlate is { } newPlate ? newPlate : latest.LicensePlate;
        var maxLoadWeightKg = canUpdateVehicle && request.Vehicle?.MaxLoadWeightKg is { } newLoad ? newLoad : latest.MaxLoadWeightKg;

        var canUpdatePersonal = rejectedKeys is null || rejectedKeys.Count == 0 || rejectedKeys.Contains(DriverApplicationItemKeys.PersonalInfo);
        var rawCitizenId = canUpdatePersonal && request.PersonalInfo is not null ? request.PersonalInfo.CitizenIdNumber : latest.CitizenIdNumber;
        var rawLicenseNumber = canUpdatePersonal && request.PersonalInfo is not null ? request.PersonalInfo.DriverLicenseNumber : latest.DriverLicenseNumber;

        var vehicleType = await DriverApplicationSubmissionHelper.ValidateVehicleTypeAndLoadAsync(
            vehicleTypes, vehicleTypeId, maxLoadWeightKg, cancellationToken);

        var (citizenId, licenseNumber, plate) = DriverApplicationSubmissionHelper.NormalizeCredentials(
            rawCitizenId, rawLicenseNumber, licensePlateInput);

        await DriverApplicationSubmissionHelper.EnsureNoDuplicatesAsync(
            connectionFactory, user.Id, citizenId, licenseNumber, plate, cancellationToken);

        profile.UpdateCredentials(citizenId, licenseNumber, urls[0], urls[1]);

        var application = DriverApplication.Create(
            profile.Id,
            profile.NextVersionNumber(),
            vehicleType.Id,
            citizenId,
            licenseNumber,
            plate,
            maxLoadWeightKg,
            urls,
            clock.GetUtcNow());

        profile.Submit(application);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return application.Id;
    }
}

