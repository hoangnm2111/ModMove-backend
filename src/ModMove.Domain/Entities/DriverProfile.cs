using ModMove.Domain.Common;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.Domain.Entities;

/// <summary>Current approved credentials and working state of a driver. Root of its applications and vehicle.</summary>
public sealed class DriverProfile : BaseAuditableEntity, IAggregateRoot
{
    private readonly List<DriverApplication> _applications = [];

    private DriverProfile() { }

    public long UserId { get; private set; }
    public string LicenseFrontUrl { get; private set; } = null!;
    public string LicenseBackUrl { get; private set; } = null!;
    public ApprovalStatus ApprovalStatus { get; private set; } = ApprovalStatus.Pending;
    public OnlineStatus OnlineStatus { get; private set; } = OnlineStatus.Offline;
    public decimal? LastLatitude { get; private set; }
    public decimal? LastLongitude { get; private set; }
    public DateTimeOffset? LastLocationAt { get; private set; }
    public string? BankName { get; private set; }
    public string? BankAccountNumber { get; private set; }
    public string? BankAccountHolder { get; private set; }

    public Vehicle? Vehicle { get; private set; }
    public IReadOnlyCollection<DriverApplication> Applications => _applications;

    public static DriverProfile Create(long userId, string licenseFrontUrl, string licenseBackUrl) =>
        new()
        {
            UserId = userId,
            LicenseFrontUrl = licenseFrontUrl,
            LicenseBackUrl = licenseBackUrl,
        };

    public int NextVersionNumber() => _applications.Count == 0 ? 1 : _applications.Max(a => a.VersionNumber) + 1;

    /// <summary>UC-DRV-05: a resubmission replaces the licence photos kept on the profile.</summary>
    public void UpdateCredentials(string licenseFrontUrl, string licenseBackUrl)
    {
        LicenseFrontUrl = licenseFrontUrl;
        LicenseBackUrl = licenseBackUrl;
    }

    /// <summary>Add a new application version; only one may be Pending at a time, and Approved profiles cannot resubmit (MSG07).</summary>
    public void Submit(DriverApplication application)
    {
        if (ApprovalStatus == ApprovalStatus.Approved)
            throw new DomainException("An approved driver application cannot be resubmitted.", "APPLICATION_NOT_EDITABLE");

        if (_applications.Any(a => a.ApplicationStatus == ApprovalStatus.Pending))
            throw new DomainException("A driver application is already pending review.", "APPLICATION_NOT_EDITABLE");

        _applications.Add(application);
        ApprovalStatus = ApprovalStatus.Pending;
    }

    public void Approve(Vehicle vehicle)
    {
        ApprovalStatus = ApprovalStatus.Approved;

        // A newer approved version updates the existing vehicle (one per driver) instead of adding a second row.
        if (Vehicle is null)
            Vehicle = vehicle;
        else
            Vehicle.UpdateFrom(vehicle);
    }

    public void Reject() => ApprovalStatus = ApprovalStatus.Rejected;

    /// <summary>GB-03: a driver can only go Online once personal and vehicle documents are Approved.</summary>
    public void GoOnline()
    {
        if (ApprovalStatus != ApprovalStatus.Approved)
            throw new DomainException("Driver documents must be approved before going online.");
        OnlineStatus = OnlineStatus.Online;
    }

    /// <summary>DECISIONS B5: a driver with an order from Accepted to InTransit stays Online until it is delivered or reassigned.</summary>
    public void GoOffline(bool hasActiveOrder = false)
    {
        if (hasActiveOrder)
            throw new DomainException("You cannot go offline while you have an order in progress.", "DRIVER_HAS_ACTIVE_ORDER");
        OnlineStatus = OnlineStatus.Offline;
    }

    /// <summary>
    /// DECISIONS B20: a mock location is refused. B19: a point less accurate than 100 m is dropped.
    /// B21: a point not newer than the saved one (a backlog point sent late) never overwrites it.
    /// </summary>
    public LocationUpdateResult UpdateLocation(decimal latitude, decimal longitude, DateTimeOffset at, bool isMocked = false, decimal? accuracyMeters = null)
    {
        EnsureRealLocation(isMocked);
        if (accuracyMeters > Constants.BusinessRules.GpsMaxAccuracyMeters)
            return LocationUpdateResult.Inaccurate;
        if (at <= LastLocationAt)
            return LocationUpdateResult.Stale;
        LastLatitude = latitude;
        LastLongitude = longitude;
        LastLocationAt = at;
        return LocationUpdateResult.Applied;
    }

    /// <summary>DECISIONS B20: refused before any other check, so the app always learns to turn the mock app off.</summary>
    public static void EnsureRealLocation(bool isMocked)
    {
        if (isMocked)
            throw new DomainException("Turn off the mock location app to share your position.", "MOCK_LOCATION");
    }

    /// <summary>
    /// DECISIONS 6.4 / GB-39: true while the last GPS point is less than 5 minutes old. A driver without a fresh point is
    /// disconnected: hidden on the Dispatcher map and shown no new orders (an order already in progress is kept).
    /// </summary>
    public bool HasFreshLocation(DateTimeOffset now) =>
        LastLocationAt is { } at && now - at < Constants.BusinessRules.GpsDisconnectAfter;

    public void LinkBankAccount(string bankName, string accountNumber, string accountHolder)
    {
        BankName = bankName;
        BankAccountNumber = accountNumber;
        BankAccountHolder = accountHolder;
    }
}
