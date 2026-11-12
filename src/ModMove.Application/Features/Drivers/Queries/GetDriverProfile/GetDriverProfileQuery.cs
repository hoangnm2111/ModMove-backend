using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Drivers.Queries.GetDriverProfile;

/// <summary>
/// UC-DRV-06, Screen #28 (Driver Profile &amp; Vehicle Screen): personal credentials, linked bank account,
/// registered vehicle (with cargo bed dimensions and certificates) and trip/rating summary of the signed-in driver.
/// </summary>
public sealed record GetDriverProfileQuery : IQuery<DriverProfileDetailResponse>;

public sealed record DriverProfileDetailResponse(
    long UserId,
    string FullName,
    string PhoneNumber,
    string Email,
    bool EmailVerified,
    DateTimeOffset CreatedAt,
    bool HasProfile,
    long? DriverProfileId,
    string? ApprovalStatus,
    string? OnlineStatus,
    string? CitizenIdNumber,
    string? DriverLicenseNumber,
    string? LicenseFrontUrl,
    string? LicenseBackUrl,
    string? CitizenIdFrontUrl,
    string? CitizenIdBackUrl,
    DriverProfileBankAccountDto? BankAccount,
    DriverVehicleDetailDto? Vehicle,
    decimal? AverageStars,
    int TotalRatings,
    int CompletedTrips);

public sealed record DriverProfileBankAccountDto(
    string BankName,
    string BankAccountNumber,
    string BankAccountHolder);

public sealed record DriverVehicleDetailDto(
    long VehicleId,
    long VehicleTypeId,
    string VehicleTypeName,
    string LicensePlate,
    decimal MaxLoadWeightKg,
    int CargoBedLengthCm,
    int CargoBedWidthCm,
    int CargoBedHeightCm,
    string CargoBedDimensions,
    string RegistrationCertUrl,
    string InspectionCertUrl,
    string VehiclePhotoUrl,
    string VehicleStatus);

