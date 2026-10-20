using ModMove.Domain.Common;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.Domain.Entities;

/// <summary>One submitted version of a driver's onboarding documents (UC-DRV-03, UC-DRV-05).</summary>
public sealed class DriverApplication : BaseAuditableEntity
{
    private DriverApplication() { }

    public long DriverProfileId { get; private set; }
    public int VersionNumber { get; private set; }
    public long VehicleTypeId { get; private set; }
    public string CitizenIdNumber { get; private set; } = null!;
    public string DriverLicenseNumber { get; private set; } = null!;
    public string LicensePlate { get; private set; } = null!;
    public decimal MaxLoadWeightKg { get; private set; }

    /// <summary>License front/back, registration, inspection, vehicle photo.</summary>
    public List<string> DocumentUrls { get; private set; } = [];

    public ApprovalStatus ApplicationStatus { get; private set; } = ApprovalStatus.Pending;

    /// <summary>Reason per rejected item (UC-ADM-09).</summary>
    public List<string>? RejectionReasons { get; private set; }

    public long? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }

    public static DriverApplication Create(
        long driverProfileId, int versionNumber, long vehicleTypeId, string citizenIdNumber,
        string driverLicenseNumber, string licensePlate, decimal maxLoadWeightKg,
        IEnumerable<string> documentUrls, DateTimeOffset submittedAt) =>
        new()
        {
            DriverProfileId = driverProfileId,
            VersionNumber = versionNumber,
            VehicleTypeId = vehicleTypeId,
            CitizenIdNumber = citizenIdNumber,
            DriverLicenseNumber = driverLicenseNumber,
            LicensePlate = licensePlate,
            MaxLoadWeightKg = maxLoadWeightKg,
            DocumentUrls = [.. documentUrls],
            SubmittedAt = submittedAt,
        };

    public void Approve(long adminId, DateTimeOffset now)
    {
        EnsurePending();
        ApplicationStatus = ApprovalStatus.Approved;
        ReviewedBy = adminId;
        ReviewedAt = now;
    }

    public void Reject(long adminId, IEnumerable<string> reasons, DateTimeOffset now)
    {
        EnsurePending();
        var list = reasons.ToList();
        if (list.Count == 0)
            throw new DomainException("A rejection needs at least one reason.");

        ApplicationStatus = ApprovalStatus.Rejected;
        RejectionReasons = list;
        ReviewedBy = adminId;
        ReviewedAt = now;
    }

    private void EnsurePending()
    {
        if (ApplicationStatus != ApprovalStatus.Pending)
            throw new DomainException("Only a pending application can be reviewed.");
    }
}
