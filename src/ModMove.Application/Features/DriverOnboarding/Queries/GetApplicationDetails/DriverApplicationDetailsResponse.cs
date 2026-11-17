namespace ModMove.Application.Features.DriverOnboarding.Queries.GetApplicationDetails;

public sealed record DriverApplicationDetailsResponse(
    long ApplicationId,
    long DriverProfileId,
    long UserId,
    string FullName,
    string PhoneNumber,
    string Email,
    string? CitizenIdNumber,
    string? DriverLicenseNumber,
    long VehicleTypeId,
    string VehicleTypeName,
    string LicensePlate,
    decimal MaxLoadWeightKg,
    int VersionNumber,
    string ApplicationStatus,
    DateTimeOffset SubmittedAt,
    long? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    IReadOnlyList<string> DocumentUrls,
    IReadOnlyList<string>? RejectionReasons,
    IReadOnlyList<ApplicationHistoryDto> History,
    IReadOnlyList<RejectedApplicationItemDto>? RejectedItems = null);

public sealed record ApplicationHistoryDto(
    long ApplicationId,
    int VersionNumber,
    string ApplicationStatus,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ReviewedAt,
    IReadOnlyList<string>? RejectionReasons,
    IReadOnlyList<RejectedApplicationItemDto>? RejectedItems = null);
