using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.UnitTests.Domain;

/// <summary>
/// Unit tests for Driver Onboarding Domain logic: DriverProfile, DriverApplication, and Vehicle.
/// Covers happy cases and edge cases (GB-03, GB-04, UC-ADM-08, UC-ADM-09).
/// </summary>
public class DriverOnboardingDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 10, 0, 0, TimeSpan.Zero);

    private static DriverProfile CreateProfile(long userId = 1) =>
        DriverProfile.Create(userId, "https://cdn/front.jpg", "https://cdn/back.jpg");

    private static DriverApplication CreateApplication(long profileId, int version = 1) =>
        DriverApplication.Create(
            profileId, version, 1L, "51C-123.45", 1000m,
            ["https://cdn/front.jpg", "https://cdn/back.jpg", "https://cdn/reg.jpg", "https://cdn/insp.jpg", "https://cdn/car.jpg"],
            Now);

    private static Vehicle CreateVehicle(long profileId) =>
        Vehicle.Create(profileId, 1L, "51C-123.45", 1000m, "250x160x160", "reg.jpg", "insp.jpg", "car.jpg");

    // ==========================================
    // 1. DriverApplication Tests
    // ==========================================

    [Fact]
    public void Approve_application_happy_path_sets_status_and_review_info()
    {
        var app = CreateApplication(1);

        app.Approve(adminId: 99, Now);

        Assert.Equal(ApprovalStatus.Approved, app.ApplicationStatus);
        Assert.Equal(99, app.ReviewedBy);
        Assert.Equal(Now, app.ReviewedAt);
    }

    [Fact]
    public void Approve_application_when_not_pending_throws_DomainException()
    {
        var app = CreateApplication(1);
        app.Approve(adminId: 99, Now);

        // Edge case: attempting to approve already approved application
        var ex = Assert.Throws<DomainException>(() => app.Approve(adminId: 100, Now.AddMinutes(5)));
        Assert.Equal("Only a pending application can be reviewed.", ex.Message);
    }

    [Fact]
    public void Reject_application_happy_path_sets_reasons_and_status()
    {
        var app = CreateApplication(1);
        var reasons = new[] { "Ảnh mờ", "Sai thông tin biển số" };

        app.Reject(adminId: 99, reasons, Now);

        Assert.Equal(ApprovalStatus.Rejected, app.ApplicationStatus);
        Assert.Equal(99, app.ReviewedBy);
        Assert.Equal(Now, app.ReviewedAt);
        Assert.Equal(reasons, app.RejectionReasons);
    }

    [Fact]
    public void Reject_application_without_reasons_throws_DomainException()
    {
        var app = CreateApplication(1);

        // Edge case: empty reasons array
        var ex = Assert.Throws<DomainException>(() => app.Reject(adminId: 99, [], Now));
        Assert.Equal("A rejection needs at least one reason.", ex.Message);
    }

    [Fact]
    public void Reject_application_when_not_pending_throws_DomainException()
    {
        var app = CreateApplication(1);
        app.Reject(adminId: 99, ["Ảnh mờ"], Now);

        // Edge case: attempting to reject already rejected application
        var ex = Assert.Throws<DomainException>(() => app.Reject(adminId: 99, ["Lý do khác"], Now.AddMinutes(5)));
        Assert.Equal("Only a pending application can be reviewed.", ex.Message);
    }

    // ==========================================
    // 2. DriverProfile Submission & Versioning Tests
    // ==========================================

    [Fact]
    public void Profile_calculates_next_version_number_correctly()
    {
        var profile = CreateProfile();
        Assert.Equal(1, profile.NextVersionNumber());

        var app1 = CreateApplication(profile.Id, 1);
        profile.Submit(app1);
        Assert.Equal(2, profile.NextVersionNumber());
    }

    [Fact]
    public void Profile_submit_happy_path_adds_application_and_sets_pending()
    {
        var profile = CreateProfile();
        var app = CreateApplication(profile.Id, 1);

        profile.Submit(app);

        Assert.Single(profile.Applications);
        Assert.Equal(ApprovalStatus.Pending, profile.ApprovalStatus);
    }

    [Fact]
    public void Profile_submit_when_another_application_is_pending_throws_DomainException()
    {
        var profile = CreateProfile();
        var app1 = CreateApplication(profile.Id, 1);
        profile.Submit(app1);

        // Edge case: submit second application while first is still pending review
        var app2 = CreateApplication(profile.Id, 2);
        var ex = Assert.Throws<DomainException>(() => profile.Submit(app2));
        Assert.Equal("A driver application is already pending review.", ex.Message);
    }

    [Fact]
    public void Profile_update_credentials_happy_path()
    {
        var profile = CreateProfile();

        profile.UpdateCredentials("https://cdn/f2.jpg", "https://cdn/b2.jpg");
        Assert.Equal("https://cdn/f2.jpg", profile.LicenseFrontUrl);
        Assert.Equal("https://cdn/b2.jpg", profile.LicenseBackUrl);
    }

    // ==========================================
    // 3. DriverProfile Approval, Vehicle Update & Online Status
    // ==========================================

    [Fact]
    public void Profile_approve_happy_path_sets_approved_and_creates_vehicle()
    {
        var profile = CreateProfile();
        var vehicle = CreateVehicle(profile.Id);

        profile.Approve(vehicle);

        Assert.Equal(ApprovalStatus.Approved, profile.ApprovalStatus);
        Assert.NotNull(profile.Vehicle);
        Assert.Equal("51C-123.45", profile.Vehicle.LicensePlate);
    }

    [Fact]
    public void Profile_approve_subsequent_version_updates_existing_vehicle_in_place()
    {
        var profile = CreateProfile();
        var vehicle1 = CreateVehicle(profile.Id);
        profile.Approve(vehicle1);

        // A newer approved version updates existing vehicle entity
        var vehicle2 = Vehicle.Create(
            profile.Id, 2L, "51D-999.99", 1500m, "300x180x180", "reg2.jpg", "insp2.jpg", "car2.jpg");
        profile.Approve(vehicle2);

        Assert.NotNull(profile.Vehicle);
        Assert.Same(vehicle1, profile.Vehicle); // Same reference updated in place
        Assert.Equal("51D-999.99", profile.Vehicle.LicensePlate);
        Assert.Equal(1500m, profile.Vehicle.MaxLoadWeightKg);
        Assert.Equal("300x180x180", profile.Vehicle.CargoBedDimensions);
    }

    [Fact]
    public void Profile_reject_sets_status_rejected()
    {
        var profile = CreateProfile();
        profile.Reject();

        Assert.Equal(ApprovalStatus.Rejected, profile.ApprovalStatus);
    }

    [Fact]
    public void Driver_can_only_go_online_when_approved_GB03()
    {
        var profile = CreateProfile();
        Assert.Equal(ApprovalStatus.Pending, profile.ApprovalStatus);

        // Edge case: Pending driver tries to go online
        var exPending = Assert.Throws<DomainException>(() => profile.GoOnline());
        Assert.Equal("Driver documents must be approved before going online.", exPending.Message);

        // Edge case: Rejected driver tries to go online
        profile.Reject();
        var exRejected = Assert.Throws<DomainException>(() => profile.GoOnline());
        Assert.Equal("Driver documents must be approved before going online.", exRejected.Message);

        // Happy case: Approved driver goes online
        profile.Approve(CreateVehicle(profile.Id));
        profile.GoOnline();
        Assert.Equal(OnlineStatus.Online, profile.OnlineStatus);

        profile.GoOffline();
        Assert.Equal(OnlineStatus.Offline, profile.OnlineStatus);
    }

    [Fact]
    public void Profile_update_location_and_bank_account()
    {
        var profile = CreateProfile();

        profile.UpdateLocation(10.762622m, 106.660172m, Now);
        Assert.Equal(10.762622m, profile.LastLatitude);
        Assert.Equal(106.660172m, profile.LastLongitude);
        Assert.Equal(Now, profile.LastLocationAt);

        profile.LinkBankAccount("Vietcombank", "1234567890", "NGUYEN VAN LAI");
        Assert.Equal("Vietcombank", profile.BankName);
        Assert.Equal("1234567890", profile.BankAccountNumber);
        Assert.Equal("NGUYEN VAN LAI", profile.BankAccountHolder);
    }

    // ==========================================
    // 4. Vehicle Tests
    // ==========================================

    [Fact]
    public void Vehicle_can_be_deactivated_and_reactivated_from_other()
    {
        var vehicle = CreateVehicle(1);
        Assert.Equal(VehicleStatus.Active, vehicle.VehicleStatus);

        vehicle.Deactivate();
        Assert.Equal(VehicleStatus.Inactive, vehicle.VehicleStatus);

        var updated = Vehicle.Create(1, 1L, "51C-888.88", 1000m, "250x160x160", "reg.jpg", "insp.jpg", "car.jpg");
        vehicle.UpdateFrom(updated);

        Assert.Equal(VehicleStatus.Active, vehicle.VehicleStatus);
        Assert.Equal("51C-888.88", vehicle.LicensePlate);
    }
}
