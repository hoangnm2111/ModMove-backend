using System.Data;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ModMove.Application;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Features.DriverOnboarding.Commands.RejectDriverApplication;
using ModMove.Application.Features.DriverOnboarding.Commands.SubmitDriverApplication;
using ModMove.Application.Features.DriverOnboarding.Queries.GetMyApplicationStatus;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

/// <summary>Driver side of onboarding: UC-DRV-03, UC-DRV-04, UC-DRV-05, GB-01, GB-04, MSG07.</summary>
public class DriverOnboardingSubmissionTests
{
    private static readonly string[] Docs =
        ["https://res.cloudinary.com/demo/image/upload/front.jpg", "https://res.cloudinary.com/demo/image/upload/back.jpg", "https://res.cloudinary.com/demo/image/upload/reg.jpg", "https://res.cloudinary.com/demo/image/upload/insp.jpg", "https://res.cloudinary.com/demo/image/upload/veh.jpg",
         "https://res.cloudinary.com/demo/image/upload/cccd-front.jpg", "https://res.cloudinary.com/demo/image/upload/cccd-back.jpg"];

    private sealed class App
    {
        public User Admin { get; } = User.Create("Admin", "0900000009", "admin@x.vn", "h:x", UserRole.Administrator, emailVerified: true);
        public User DriverUser { get; }
        public FakeSql Sql { get; } = new();
        public InMemoryDriverProfiles Profiles { get; } = new();
        public InMemoryRepository<VehicleType> VehicleTypes { get; } = new();
        public CountingUnitOfWork UnitOfWork { get; } = new();
        public MutableTime Time { get; } = new(TestApp.Start);
        public ISender DriverSender { get; }
        public ISender AdminSender { get; }

        public App(UserRole role = UserRole.Driver, bool emailVerified = true)
        {
            DriverUser = User.Create("Lai Xe", "0911222333", "driver@x.vn", "h:x", role, emailVerified);
            var users = new InMemoryUsers(Admin, DriverUser);
            var type = VehicleType.Create("Xe tai 1 tan", 1000, 250, 160, 160, 80_000, 12_000, 120_000);
            TestIds.Set(type, 1);
            VehicleTypes.Add(type);

            DriverSender = Build(users, DriverUser.Id);
            AdminSender = Build(users, Admin.Id);
        }

        private ISender Build(InMemoryUsers users, long signedInId) => new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddSingleton<ISqlConnectionFactory>(Sql)
            .AddSingleton<IUserRepository>(users)
            .AddSingleton<IDriverProfileRepository>(Profiles)
            .AddSingleton<IRepository<VehicleType>>(VehicleTypes)
            .AddSingleton<IUnitOfWork>(UnitOfWork)
            .AddSingleton<IFileStorage>(new FakeFileStorage())
            .AddSingleton<ICurrentUserService>(new FakeCurrentUser(signedInId))
            .AddSingleton<TimeProvider>(Time)
            .BuildServiceProvider()
            .GetRequiredService<ISender>();

        public void NoDuplicates() => Sql.Result = new DataTable();

        public void DuplicatesFound(params string[] fields)
        {
            var dt = new DataTable();
            dt.Columns.Add("field", typeof(string));
            foreach (var f in fields) dt.Rows.Add(f);
            Sql.Result = dt;
        }
    }

    private static SubmitDriverApplicationCommand Command(string plate = "51C-111.22", decimal weight = 900m, long typeId = 1) =>
        new(typeId, "012345678901", "B2123456789", plate, weight, Docs);

    // ---------- UC-DRV-03: first submission ----------

    [Fact]
    public async Task First_submission_creates_profile_with_version_1_pending()
    {
        var app = new App();
        app.NoDuplicates();

        await app.DriverSender.Send(Command("51c-111.22"));

        var profile = Assert.Single(app.Profiles.Items);
        Assert.Equal(app.DriverUser.Id, profile.UserId);
        Assert.Equal(ApprovalStatus.Pending, profile.ApprovalStatus);
        Assert.Equal(Docs[0], profile.LicenseFrontUrl);
        Assert.Equal(Docs[1], profile.LicenseBackUrl);

        var application = Assert.Single(profile.Applications);
        Assert.Equal(1, application.VersionNumber);
        Assert.Equal(ApprovalStatus.Pending, application.ApplicationStatus);
        Assert.Equal("51C-111.22", application.LicensePlate); // normalised
        Assert.Equal(TestApp.Start, application.SubmittedAt);
        Assert.Equal(Docs, application.DocumentUrls);
        Assert.Equal(1, app.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Duplicate_checks_exclude_the_callers_own_profile_and_bind_normalised_values()
    {
        var app = new App();
        app.NoDuplicates();

        await app.DriverSender.Send(Command("51c-111.22"));

        Assert.Contains("user_id <> @UserId", app.Sql.LastSql);
        Assert.Equal("51C-111.22", app.Sql.LastParameters["LicensePlate"]);
        Assert.Equal(app.DriverUser.Id, app.Sql.LastParameters["UserId"]);
    }

    [Fact]
    public async Task Duplicate_citizen_id_or_plate_returns_conflict_and_saves_nothing()
    {
        var app = new App();
        app.DuplicatesFound("CitizenIdNumber", "LicensePlate");

        var ex = await Assert.ThrowsAsync<ConflictException>(() => app.DriverSender.Send(Command()));

        Assert.Contains("CitizenIdNumber", ex.Message);
        Assert.Contains("LicensePlate", ex.Message);
        Assert.Empty(app.Profiles.Items);
        Assert.Equal(0, app.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Unknown_vehicle_type_is_not_found()
    {
        var app = new App();
        await Assert.ThrowsAsync<NotFoundException>(() => app.DriverSender.Send(Command(typeId: 99)));
    }

    [Fact]
    public async Task Disabled_vehicle_type_is_refused()
    {
        var app = new App();
        app.VehicleTypes.Items[0].SetActive(false);
        await Assert.ThrowsAsync<DomainException>(() => app.DriverSender.Send(Command()));
    }

    [Fact]
    public async Task Load_above_the_vehicle_type_maximum_is_refused()
    {
        var app = new App();
        app.NoDuplicates();
        await Assert.ThrowsAsync<DomainException>(() => app.DriverSender.Send(Command(weight: 1500m)));
        Assert.Empty(app.Profiles.Items);
    }

    [Fact]
    public async Task Only_drivers_can_submit()
    {
        var app = new App(UserRole.Customer);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => app.DriverSender.Send(Command()));
    }

    [Fact]
    public async Task Unverified_email_is_refused()
    {
        var app = new App(emailVerified: false);
        var ex = await Assert.ThrowsAsync<ForbiddenAccessException>(() => app.DriverSender.Send(Command()));
        Assert.Equal("EMAIL_NOT_VERIFIED", ex.Code);
    }

    [Fact]
    public async Task Invalid_input_is_rejected_by_the_validation_pipeline()
    {
        var app = new App();
        await Assert.ThrowsAsync<ValidationException>(() =>
            app.DriverSender.Send(Command() with { DocumentUrls = Docs[..3] }));
    }

    // ---------- MSG07: Pending / Approved are not editable ----------

    [Fact]
    public async Task Second_submission_while_pending_is_refused_with_code()
    {
        var app = new App();
        app.NoDuplicates();
        await app.DriverSender.Send(Command());

        var ex = await Assert.ThrowsAsync<DomainException>(() => app.DriverSender.Send(Command()));

        Assert.Equal("APPLICATION_NOT_EDITABLE", ex.Code);
        Assert.Single(app.Profiles.Items[0].Applications);
    }

    [Fact]
    public async Task Submission_after_approval_is_refused()
    {
        var app = new App();
        app.NoDuplicates();
        await app.DriverSender.Send(Command());
        var profile = app.Profiles.Items[0];
        profile.Applications.Single().Approve(app.Admin.Id, TestApp.Start);
        profile.Approve(Vehicle.Create(profile.Id, 1, "51C-111.22", 900, "250x160x160", "r", "i", "p"));

        var ex = await Assert.ThrowsAsync<DomainException>(() => app.DriverSender.Send(Command()));
        Assert.Equal("APPLICATION_NOT_EDITABLE", ex.Code);
    }

    // ---------- UC-DRV-05: resubmission after rejection ----------

    [Fact]
    public async Task Resubmission_after_rejection_creates_version_2_and_keeps_history()
    {
        var app = new App();
        app.NoDuplicates();
        await app.DriverSender.Send(Command());
        var profile = app.Profiles.Items[0];
        TestIds.Set(profile.Applications.Single(), 10);

        // Admin rejects through the real handler (reviewer slice shipped earlier).
        var adminSender = new ServiceCollection()
            .AddLogging().AddApplication()
            .AddSingleton<IUserRepository>(new InMemoryUsers(app.Admin, app.DriverUser))
            .AddSingleton<IDriverProfileRepository>(app.Profiles)
            .AddSingleton<IUnitOfWork>(app.UnitOfWork)
            .AddSingleton<ICurrentUserService>(new FakeCurrentUser(app.Admin.Id))
            .AddSingleton<TimeProvider>(app.Time)
            .BuildServiceProvider().GetRequiredService<ISender>();
        await adminSender.Send(new RejectDriverApplicationCommand(10, ["Inspection certificate expired"]));
        Assert.Equal(ApprovalStatus.Rejected, profile.ApprovalStatus);

        app.Time.Now = TestApp.Start.AddDays(1);
        var newDocs = Docs.ToArray();
        newDocs[3] = "https://res.cloudinary.com/demo/image/upload/insp-new.jpg";
        await app.DriverSender.Send(Command() with { DocumentUrls = newDocs, DriverLicenseNumber = "B2999999999" });

        Assert.Equal(ApprovalStatus.Pending, profile.ApprovalStatus);
        Assert.Equal(2, profile.Applications.Count);
        var first = profile.Applications.Single(a => a.VersionNumber == 1);
        var second = profile.Applications.Single(a => a.VersionNumber == 2);
        Assert.Equal(ApprovalStatus.Rejected, first.ApplicationStatus);          // earlier decision kept
        Assert.Equal(ApprovalStatus.Pending, second.ApplicationStatus);
        Assert.Equal("https://res.cloudinary.com/demo/image/upload/insp-new.jpg", second.DocumentUrls[3]);
        Assert.Equal(TestApp.Start.AddDays(1), second.SubmittedAt);
        Assert.Equal("B2999999999", profile.DriverLicenseNumber);                // credentials follow the latest version
        Assert.Single(app.Profiles.Items);                                       // no second profile
    }

    // ---------- UC-DRV-04: status ----------

    private static DataTable StatusRow(string status, string? reasons = null, DateTimeOffset? reviewedAt = null)
    {
        var dt = new DataTable();
        dt.Columns.Add("ApplicationId", typeof(long));
        dt.Columns.Add("VersionNumber", typeof(int));
        dt.Columns.Add("ApplicationStatus", typeof(string));
        dt.Columns.Add("SubmittedAt", typeof(DateTimeOffset));
        dt.Columns.Add("ReviewedAt", typeof(DateTimeOffset));
        dt.Columns.Add("VehicleTypeId", typeof(long));
        dt.Columns.Add("VehicleTypeName", typeof(string));
        dt.Columns.Add("LicensePlate", typeof(string));
        dt.Columns.Add("MaxLoadWeightKg", typeof(decimal));
        dt.Columns.Add("CitizenIdNumber", typeof(string));
        dt.Columns.Add("DriverLicenseNumber", typeof(string));
        dt.Columns.Add("DocumentUrlsJson", typeof(string));
        dt.Columns.Add("RejectionReasonsJson", typeof(string));
        dt.Rows.Add(7L, 2, status, TestApp.Start, (object?)reviewedAt ?? DBNull.Value, 1L, "Xe tai 1 tan", "51C-111.22", 900m,
            "012345678901", "B2123456789", JsonSerializer.Serialize(Docs), (object?)reasons ?? DBNull.Value);
        return dt;
    }

    private static void RouteStatusQueries(App app, DataTable latest) =>
        app.Sql.CustomQuery = (sql, _) => sql.Contains("account_status")
            ? new DataTable().Also(t => { t.Columns.Add("account_status", typeof(string)); t.Rows.Add("Active"); })
            : latest;

    [Fact]
    public async Task Status_without_application_says_so_and_allows_submit()
    {
        var app = new App();
        RouteStatusQueries(app, new DataTable());

        var status = await app.DriverSender.Send(new GetMyApplicationStatusQuery());

        Assert.False(status.HasApplication);
        Assert.True(status.CanSubmit);
        Assert.Null(status.ProfileApprovalStatus);
        Assert.Empty(status.DocumentUrls);
    }

    [Fact]
    public async Task Status_pending_is_read_only()
    {
        var app = new App();
        RouteStatusQueries(app, StatusRow("Pending"));

        var status = await app.DriverSender.Send(new GetMyApplicationStatusQuery());

        Assert.True(status.HasApplication);
        Assert.False(status.CanSubmit);
        Assert.Equal("Pending", status.ProfileApprovalStatus);
        Assert.Equal(2, status.LatestVersionNumber);
        Assert.Equal(Docs, status.DocumentUrls);
        Assert.Null(status.RejectionReasons);
    }

    [Fact]
    public async Task Status_rejected_lists_reasons_and_allows_resubmit()
    {
        var app = new App();
        var reasons = JsonSerializer.Serialize(new[] { "Blurry licence photo", "Plate mismatch" });
        RouteStatusQueries(app, StatusRow("Rejected", reasons, TestApp.Start.AddHours(3)));

        var status = await app.DriverSender.Send(new GetMyApplicationStatusQuery());

        Assert.True(status.CanSubmit);
        Assert.Equal(["Blurry licence photo", "Plate mismatch"], status.RejectionReasons);
        Assert.Equal(TestApp.Start.AddHours(3), status.ReviewedAt);
    }

    [Fact]
    public async Task Status_for_inactive_account_is_forbidden()
    {
        var app = new App();
        app.Sql.CustomQuery = (sql, _) => sql.Contains("account_status")
            ? new DataTable().Also(t => { t.Columns.Add("account_status", typeof(string)); t.Rows.Add("Locked"); })
            : new DataTable();

        var ex = await Assert.ThrowsAsync<ForbiddenAccessException>(() => app.DriverSender.Send(new GetMyApplicationStatusQuery()));
        Assert.Equal("ACCOUNT_INACTIVE", ex.Code);
    }
}

internal static class DataTableExtensions
{
    public static DataTable Also(this DataTable table, Action<DataTable> configure)
    {
        configure(table);
        return table;
    }
}

