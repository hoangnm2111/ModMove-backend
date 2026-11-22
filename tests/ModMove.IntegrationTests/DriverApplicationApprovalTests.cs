using System.Data;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ModMove.Application;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Features.DriverOnboarding;
using ModMove.Application.Features.DriverOnboarding.Commands.ApproveDriverApplication;
using ModMove.Application.Features.DriverOnboarding.Commands.RejectDriverApplication;
using ModMove.Application.Features.DriverOnboarding.Queries.GetApplicationDetails;
using ModMove.Application.Features.DriverOnboarding.Queries.GetPendingApplications;
using ModMove.Domain.Constants;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;
using ModMove.Infrastructure.Persistence;

namespace ModMove.IntegrationTests;

/// <summary>Screens #33 &amp; #34: UC-ADM-08, UC-ADM-09, GB-03, GB-04, GB-16, TEAM-PLAN line 74.</summary>
public class DriverApplicationApprovalTests
{
    private sealed class DriverOnboardingApp
    {
        public User Admin { get; } = User.Create("Admin", "0900000009", "admin@x.vn", "h:x", UserRole.Administrator, emailVerified: true);
        public User DriverUser { get; } = User.Create("Nguyễn Văn Lái", "0911222333", "driver@x.vn", "h:x", UserRole.Driver, emailVerified: true);
        public FakeSql Sql { get; } = new();
        public FakeFileStorage Files { get; } = new();
        public InMemoryUsers Users { get; }
        public InMemoryDriverProfiles DriverProfiles { get; } = new();
        public InMemoryDriverWallets DriverWallets { get; } = new();
        public InMemoryRepository<VehicleType> VehicleTypes { get; } = new();
        public CountingUnitOfWork UnitOfWork { get; } = new();
        public CapturingPushNotifications Push { get; } = new();
        public CapturingRealtimeNotifier Realtime { get; } = new();
        public MutableTime Time { get; } = new(TestApp.Start);
        public ISender Sender { get; }

        public DriverOnboardingApp(bool signedInAsAdmin = true)
        {
            Users = new InMemoryUsers(Admin, DriverUser);
            DriverUser.SetFcmToken("fcm-driver-token-123");

            var vehicleType = VehicleType.Create("Xe tải 1 tấn", 1000, 250, 160, 160, 80_000, 12_000, 120_000);
            TestIds.Set(vehicleType, 1);
            VehicleTypes.Add(vehicleType);

            Sender = new ServiceCollection()
                .AddLogging()
                .AddApplication()
                .AddSingleton<ISqlConnectionFactory>(Sql)
                .AddSingleton<IFileStorage>(Files)
                .AddSingleton<IUserRepository>(Users)
                .AddSingleton<IDriverProfileRepository>(DriverProfiles)
                .AddSingleton<IDriverWalletRepository>(DriverWallets)
                .AddSingleton<IRepository<VehicleType>>(VehicleTypes)
                .AddSingleton<IUnitOfWork>(UnitOfWork)
                .AddSingleton<IPushNotificationService>(Push)
                .AddSingleton<IRealtimeNotifier>(Realtime)
                .AddSingleton<ICurrentUserService>(new FakeCurrentUser(signedInAsAdmin ? Admin.Id : null))
                .AddSingleton<TimeProvider>(Time)
                .BuildServiceProvider()
                .GetRequiredService<ISender>();
        }
    }

    // ==========================================
    // 1. Pending Queue Query Tests (UC-ADM-08)
    // ==========================================

    [Fact]
    public async Task Admin_can_query_pending_driver_applications_queue_FIFO()
    {
        var app = new DriverOnboardingApp();

        var dt = new DataTable();
        dt.Columns.Add("ApplicationId", typeof(long));
        dt.Columns.Add("DriverProfileId", typeof(long));
        dt.Columns.Add("FullName", typeof(string));
        dt.Columns.Add("PhoneNumber", typeof(string));
        dt.Columns.Add("VehicleTypeName", typeof(string));
        dt.Columns.Add("LicensePlate", typeof(string));
        dt.Columns.Add("VersionNumber", typeof(int));
        dt.Columns.Add("SubmittedAt", typeof(DateTimeOffset));

        dt.Rows.Add(10L, 1L, "Nguyễn Văn Lái", "0911222333", "Xe tải 1 tấn", "51C-111.22", 1, TestApp.Start);
        dt.Rows.Add(11L, 2L, "Trần Văn Tài", "0922333444", "Xe van 500kg", "51D-333.44", 1, TestApp.Start.AddHours(2));

        app.Sql.Result = dt;
        app.Sql.ScalarResult = 2; // total count

        var result = await app.Sender.Send(new GetPendingApplicationsQuery(Page: 1, PageSize: 10));

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(10L, result.Items[0].ApplicationId);
        Assert.Equal("51C-111.22", result.Items[0].LicensePlate);
        Assert.Contains("application_status = 'Pending'", app.Sql.LastSql);
        Assert.Contains("ORDER BY da.submitted_at ASC", app.Sql.LastSql);
    }

    // ==========================================
    // 2. Application Detail Query Tests (UC-ADM-09)
    // ==========================================

    [Fact]
    public async Task Admin_can_view_driver_application_details_with_document_urls_and_history()
    {
        var app = new DriverOnboardingApp();

        var detailDt = new DataTable();
        detailDt.Columns.Add("ApplicationId", typeof(long));
        detailDt.Columns.Add("DriverProfileId", typeof(long));
        detailDt.Columns.Add("UserId", typeof(long));
        detailDt.Columns.Add("FullName", typeof(string));
        detailDt.Columns.Add("PhoneNumber", typeof(string));
        detailDt.Columns.Add("Email", typeof(string));
        detailDt.Columns.Add("VehicleTypeId", typeof(long));
        detailDt.Columns.Add("VehicleTypeName", typeof(string));
        detailDt.Columns.Add("LicensePlate", typeof(string));
        detailDt.Columns.Add("MaxLoadWeightKg", typeof(decimal));
        detailDt.Columns.Add("VersionNumber", typeof(int));
        detailDt.Columns.Add("ApplicationStatus", typeof(string));
        detailDt.Columns.Add("SubmittedAt", typeof(DateTimeOffset));
        detailDt.Columns.Add("ReviewedBy", typeof(long));
        detailDt.Columns.Add("ReviewedAt", typeof(DateTimeOffset));
        detailDt.Columns.Add("DocumentUrlsJson", typeof(string));
        detailDt.Columns.Add("RejectionReasonsJson", typeof(string));

        var docsJson = JsonSerializer.Serialize(new[]
        {
            "https://cdn.example.com/license-front.jpg",
            "https://cdn.example.com/license-back.jpg",
            "https://cdn.example.com/reg-cert.jpg",
            "https://cdn.example.com/insp-cert.jpg",
            "https://cdn.example.com/vehicle.jpg",
            "https://res.cloudinary.com/demo/image/authenticated/modmove/drivers/2/documents/6.jpg",
        });

        detailDt.Rows.Add(
            10L, 1L, app.DriverUser.Id, "Nguyễn Văn Lái", "0911222333", "driver@x.vn",
            1L, "Xe tải 1 tấn", "51C-111.22", 1000m,
            2, "Pending", TestApp.Start.AddDays(1), null, null, docsJson, null);

        var historyDt = new DataTable();
        historyDt.Columns.Add("ApplicationId", typeof(long));
        historyDt.Columns.Add("VersionNumber", typeof(int));
        historyDt.Columns.Add("ApplicationStatus", typeof(string));
        historyDt.Columns.Add("SubmittedAt", typeof(DateTimeOffset));
        historyDt.Columns.Add("ReviewedAt", typeof(DateTimeOffset));
        historyDt.Columns.Add("RejectionReasonsJson", typeof(string));

        var prevReasonsJson = JsonSerializer.Serialize(new[] { "Ảnh giấy phép lái xe bị mờ" });
        historyDt.Rows.Add(10L, 2, "Pending", TestApp.Start.AddDays(1), null, null);
        historyDt.Rows.Add(9L, 1, "Rejected", TestApp.Start, TestApp.Start.AddHours(4), prevReasonsJson);

        app.Sql.CustomQuery = (sql, _) =>
        {
            if (sql.Contains("WHERE da.id = @ApplicationId"))
                return detailDt;
            if (sql.Contains("WHERE driver_profile_id = @DriverProfileId"))
                return historyDt;
            return null;
        };

        var response = await app.Sender.Send(new GetApplicationDetailsQuery(10));

        Assert.Equal(10L, response.ApplicationId);
        Assert.Equal("Nguyễn Văn Lái", response.FullName);
        Assert.Equal("51C-111.22", response.LicensePlate);
        Assert.Equal(6, response.DocumentUrls.Count);
        Assert.Equal("https://cdn.example.com/license-front.jpg", response.DocumentUrls[0]); // an older public photo stays as it is
        Assert.EndsWith("?signed=10min", response.DocumentUrls[5]); // NFR 2.3: a private document comes back as a short-lived link
        Assert.Equal(2, response.History.Count);
        Assert.Equal("Rejected", response.History[1].ApplicationStatus);
        Assert.NotNull(response.History[1].RejectionReasons);
        Assert.Equal("Ảnh giấy phép lái xe bị mờ", response.History[1].RejectionReasons![0]);
    }

    [Fact]
    public async Task Admin_viewing_non_existent_application_throws_NotFoundException()
    {
        var app = new DriverOnboardingApp();
        app.Sql.Result = new DataTable(); // empty result

        await Assert.ThrowsAsync<NotFoundException>(() =>
            app.Sender.Send(new GetApplicationDetailsQuery(999)));
    }

    // ==========================================
    // 3. Application Approval Tests (UC-ADM-09, GB-16, Plan 74)
    // ==========================================

    [Fact]
    public async Task Admin_can_approve_pending_driver_application()
    {
        var app = new DriverOnboardingApp();

        // Setup driver profile and pending application
        var profile = DriverProfile.Create(app.DriverUser.Id, "front.jpg", "back.jpg");
        TestIds.Set(profile, 1);

        var application = DriverApplication.Create(
            profile.Id, 1, 1L, "51C-999.88", 1000m,
            ["front.jpg", "back.jpg", "reg.jpg", "insp.jpg", "veh.jpg"], TestApp.Start);
        TestIds.Set(application, 10);
        profile.Submit(application);

        app.DriverProfiles.Add(profile);

        // Act: Admin approves application
        await app.Sender.Send(new ApproveDriverApplicationCommand(10));

        // Assert
        Assert.Equal(ApprovalStatus.Approved, application.ApplicationStatus);
        Assert.Equal(app.Admin.Id, application.ReviewedBy);
        Assert.Equal(TestApp.Start, application.ReviewedAt);

        Assert.Equal(ApprovalStatus.Approved, profile.ApprovalStatus);
        Assert.NotNull(profile.Vehicle);
        Assert.Equal("51C-999.88", profile.Vehicle!.LicensePlate);
        Assert.Equal("250x160x160", profile.Vehicle.CargoBedDimensions);
        Assert.Equal("reg.jpg", profile.Vehicle.RegistrationCertUrl);
        Assert.Equal("insp.jpg", profile.Vehicle.InspectionCertUrl);
        Assert.Equal("veh.jpg", profile.Vehicle.VehiclePhotoUrl);

        // Verify DriverWallet is provisioned on first approval (Rule GB-16, TEAM-PLAN 74)
        Assert.Single(app.DriverWallets.Items);
        var wallet = app.DriverWallets.Items[0];
        Assert.Equal(profile.Id, wallet.DriverProfileId);
        Assert.Equal(0m, wallet.DepositBalance);
        Assert.Equal(0m, wallet.EarningsBalance);
        Assert.Equal(WalletStatus.Active, wallet.WalletStatus);

        Assert.True(app.UnitOfWork.Saves > 0);

        // Verify FCM notification was sent
        Assert.Single(app.Push.Sent);
        Assert.Equal("fcm-driver-token-123", app.Push.Sent[0].Token);
        Assert.Equal("Hồ sơ tài xế đã được phê duyệt", app.Push.Sent[0].Title);
    }

    [Fact]
    public async Task Admin_cannot_approve_already_approved_application()
    {
        var app = new DriverOnboardingApp();

        var profile = DriverProfile.Create(app.DriverUser.Id, "front.jpg", "back.jpg");
        TestIds.Set(profile, 1);

        var application = DriverApplication.Create(
            profile.Id, 1, 1L, "51C-999.88", 1000m,
            ["front.jpg", "back.jpg", "reg.jpg", "insp.jpg", "veh.jpg"], TestApp.Start);
        TestIds.Set(application, 10);
        profile.Submit(application);
        application.Approve(app.Admin.Id, TestApp.Start); // already approved

        app.DriverProfiles.Add(profile);

        // Act & Assert: DomainException is thrown
        await Assert.ThrowsAsync<DomainException>(() =>
            app.Sender.Send(new ApproveDriverApplicationCommand(10)));
    }

    [Fact]
    public async Task Admin_approving_non_existent_application_throws_NotFoundException()
    {
        var app = new DriverOnboardingApp();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            app.Sender.Send(new ApproveDriverApplicationCommand(999)));
    }

    // ==========================================
    // 4. Application Rejection Tests (UC-ADM-09)
    // ==========================================

    [Fact]
    public async Task Admin_can_reject_pending_driver_application_with_reasons()
    {
        var app = new DriverOnboardingApp();

        var profile = DriverProfile.Create(app.DriverUser.Id, "front.jpg", "back.jpg");
        TestIds.Set(profile, 1);

        var application = DriverApplication.Create(
            profile.Id, 1, 1L, "51C-999.88", 1000m,
            ["front.jpg", "back.jpg", "reg.jpg", "insp.jpg", "veh.jpg"], TestApp.Start);
        TestIds.Set(application, 10);
        profile.Submit(application);

        app.DriverProfiles.Add(profile);

        var reasons = new[] { "Ảnh giấy đăng ký xe bị lóa", "Biển số xe trong ảnh không khớp thông tin nhập" };

        // Act: Admin rejects application
        await app.Sender.Send(new RejectDriverApplicationCommand(10, reasons));

        // Assert
        Assert.Equal(ApprovalStatus.Rejected, application.ApplicationStatus);
        Assert.Equal(app.Admin.Id, application.ReviewedBy);
        Assert.Equal(TestApp.Start, application.ReviewedAt);
        Assert.NotNull(application.RejectionReasons);
        Assert.Equal(2, application.RejectionReasons!.Count);
        Assert.Equal("Ảnh giấy đăng ký xe bị lóa", application.RejectionReasons[0]);

        Assert.Equal(ApprovalStatus.Rejected, profile.ApprovalStatus);
        Assert.True(app.UnitOfWork.Saves > 0);

        // Verify FCM notification was sent
        Assert.Single(app.Push.Sent);
        Assert.Equal("fcm-driver-token-123", app.Push.Sent[0].Token);
        Assert.Equal("Hồ sơ tài xế bị từ chối", app.Push.Sent[0].Title);
    }

    [Fact]
    public async Task Admin_rejecting_without_reasons_fails_validation()
    {
        var app = new DriverOnboardingApp();

        // Act & Assert: ValidationBehavior rejects empty reasons
        await Assert.ThrowsAsync<ValidationException>(() =>
            app.Sender.Send(new RejectDriverApplicationCommand(10, [])));
    }

    [Fact]
    public async Task Admin_queries_return_submittedAt_as_UTC_without_7h_offset_and_structured_rejected_items()
    {
        SqlConnectionFactory.EnsureTypeHandlersRegistered();
        var app = new DriverOnboardingApp();
        var mysqlUtcWallClock = new DateTime(2026, 11, 17, 7, 0, 0, DateTimeKind.Unspecified); // 14:00 VN

        var detailDt = new DataTable();
        detailDt.Columns.Add("ApplicationId", typeof(long));
        detailDt.Columns.Add("DriverProfileId", typeof(long));
        detailDt.Columns.Add("UserId", typeof(long));
        detailDt.Columns.Add("FullName", typeof(string));
        detailDt.Columns.Add("PhoneNumber", typeof(string));
        detailDt.Columns.Add("Email", typeof(string));
        detailDt.Columns.Add("VehicleTypeId", typeof(long));
        detailDt.Columns.Add("VehicleTypeName", typeof(string));
        detailDt.Columns.Add("LicensePlate", typeof(string));
        detailDt.Columns.Add("MaxLoadWeightKg", typeof(decimal));
        detailDt.Columns.Add("VersionNumber", typeof(int));
        detailDt.Columns.Add("ApplicationStatus", typeof(string));
        detailDt.Columns.Add("SubmittedAt", typeof(DateTime));
        detailDt.Columns.Add("ReviewedBy", typeof(long));
        detailDt.Columns.Add("ReviewedAt", typeof(DateTime));
        detailDt.Columns.Add("DocumentUrlsJson", typeof(string));
        detailDt.Columns.Add("RejectionReasonsJson", typeof(string));
        detailDt.Columns.Add("RejectedItemsJson", typeof(string));

        var rejectedItemsJson = JsonSerializer.Serialize(
            new[] { new RejectedApplicationItemDto(DriverApplicationItemKeys.VehicleRegistration, "Ảnh đăng ký mờ") },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        detailDt.Rows.Add(
            10L, 1L, app.DriverUser.Id, "Nguyễn Văn Lái", "0911222333", "driver@x.vn",
            1L, "Xe tải 1 tấn", "51C-111.22", 1000m,
            1, "Rejected", mysqlUtcWallClock, app.Admin.Id, mysqlUtcWallClock,
            JsonSerializer.Serialize(new[] { "a.jpg" }),
            JsonSerializer.Serialize(new[] { "Ảnh đăng ký mờ" }),
            rejectedItemsJson);

        var historyDt = new DataTable();
        historyDt.Columns.Add("ApplicationId", typeof(long));
        historyDt.Columns.Add("VersionNumber", typeof(int));
        historyDt.Columns.Add("ApplicationStatus", typeof(string));
        historyDt.Columns.Add("SubmittedAt", typeof(DateTime));
        historyDt.Columns.Add("ReviewedAt", typeof(DateTime));
        historyDt.Columns.Add("RejectionReasonsJson", typeof(string));
        historyDt.Columns.Add("RejectedItemsJson", typeof(string));
        historyDt.Rows.Add(10L, 1, "Rejected", mysqlUtcWallClock, mysqlUtcWallClock,
            JsonSerializer.Serialize(new[] { "Ảnh đăng ký mờ" }), rejectedItemsJson);

        app.Sql.CustomQuery = (sql, _) => sql.Contains("WHERE da.id = @ApplicationId") ? detailDt : historyDt;

        var detail = await app.Sender.Send(new GetApplicationDetailsQuery(10));

        Assert.Equal(new DateTimeOffset(2026, 11, 17, 7, 0, 0, TimeSpan.Zero), detail.SubmittedAt);
        Assert.Equal(TimeSpan.Zero, detail.SubmittedAt.Offset);
        var item = Assert.Single(detail.RejectedItems!);
        Assert.Equal((DriverApplicationItemKeys.VehicleRegistration, "Ảnh đăng ký mờ"), (item.Key, item.Reason));
        Assert.Single(detail.History[0].RejectedItems!);
    }

    [Fact]
    public async Task Admin_can_reject_with_structured_rejected_items_only()
    {
        var app = new DriverOnboardingApp();
        var profile = DriverProfile.Create(app.DriverUser.Id, "front.jpg", "back.jpg");
        TestIds.Set(profile, 1);

        var application = DriverApplication.Create(
            profile.Id, 1, 1L, "51C-999.88", 1000m,
            ["front.jpg", "back.jpg", "reg.jpg", "insp.jpg", "veh.jpg", "cid-f.jpg", "cid-b.jpg"], TestApp.Start);
        TestIds.Set(application, 10);
        profile.Submit(application);
        app.DriverProfiles.Add(profile);

        await app.Sender.Send(new RejectDriverApplicationCommand(
            10,
            RejectedItems:
            [
                new("citizenIdFront", "CCCD mặt trước bị mất góc"),
                new("vehicleInfo", "Sai tải trọng xe"),
            ]));

        Assert.Equal(ApprovalStatus.Rejected, application.ApplicationStatus);
        Assert.NotNull(application.RejectedItems);
        Assert.Equal(2, application.RejectedItems!.Count);
        Assert.Equal(DriverApplicationItemKeys.CitizenIdFront, application.RejectedItems[0].Key);
        Assert.Equal(DriverApplicationItemKeys.VehicleInfo, application.RejectedItems[1].Key);
        Assert.Equal(["CCCD mặt trước bị mất góc", "Sai tải trọng xe"], application.RejectionReasons!);
    }
}
