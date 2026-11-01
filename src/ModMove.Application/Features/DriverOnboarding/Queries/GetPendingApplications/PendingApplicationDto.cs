namespace ModMove.Application.Features.DriverOnboarding.Queries.GetPendingApplications;

public sealed record PendingApplicationDto
{
    public long ApplicationId { get; init; }
    public long DriverProfileId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string VehicleTypeName { get; init; } = string.Empty;
    public string LicensePlate { get; init; } = string.Empty;
    public int VersionNumber { get; init; }
    public DateTimeOffset SubmittedAt { get; init; }

    public PendingApplicationDto() { }

    public PendingApplicationDto(
        long applicationId,
        long driverProfileId,
        string fullName,
        string phoneNumber,
        string vehicleTypeName,
        string licensePlate,
        int versionNumber,
        DateTimeOffset submittedAt)
    {
        ApplicationId = applicationId;
        DriverProfileId = driverProfileId;
        FullName = fullName;
        PhoneNumber = phoneNumber;
        VehicleTypeName = vehicleTypeName;
        LicensePlate = licensePlate;
        VersionNumber = versionNumber;
        SubmittedAt = submittedAt;
    }
}
