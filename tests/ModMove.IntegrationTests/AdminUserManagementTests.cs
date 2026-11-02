using System.Data;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ModMove.Application;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Features.Auth.Commands.Login;
using ModMove.Application.Features.Users.Commands.CreateDispatcher;
using ModMove.Application.Features.Users.Commands.UpdateUserStatus;
using ModMove.Application.Features.Users.Queries.GetUserDetail;
using ModMove.Application.Features.Users.Queries.GetUsers;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

/// <summary>Admin Account Management: UC-ADM-04, UC-ADM-05, UC-ADM-06, UC-ADM-07, DECISIONS B7, B8, 8.2, MSG65.</summary>
public class AdminUserManagementTests
{
    private sealed class AdminUsersApp
    {
        public User Admin { get; } = User.Create("Admin Hệ Thống", "0900000001", "admin@modmove.vn", "h:ModMove@123", UserRole.Administrator, emailVerified: true);
        public User Customer { get; } = User.Create("Nguyễn Khách Hàng", "0911111111", "customer@modmove.vn", "h:ModMove@123", UserRole.Customer, emailVerified: true);
        public User Driver { get; } = User.Create("Trần Tài Xế", "0922222222", "driver@modmove.vn", "h:ModMove@123", UserRole.Driver, emailVerified: true);
        public FakeSql Sql { get; } = new();
        public InMemoryUsers Users { get; }
        public InMemoryDriverProfiles DriverProfiles { get; } = new();
        public InMemoryRefreshTokens RefreshTokens { get; } = new();
        public CountingUnitOfWork UnitOfWork { get; } = new();
        public CapturingEmailSender Email { get; } = new();
        public MutableTime Time { get; } = new(TestApp.Start);
        public ISender Sender { get; }

        public AdminUsersApp(bool signedInAsAdmin = true)
        {
            Users = new InMemoryUsers(Admin, Customer, Driver);
            Customer.SetFcmToken("fcm-customer-token");
            Driver.SetFcmToken("fcm-driver-token");

            Sender = new ServiceCollection()
                .AddLogging()
                .AddApplication()
                .AddSingleton<ISqlConnectionFactory>(Sql)
                .AddSingleton<IUserRepository>(Users)
                .AddSingleton<IDriverProfileRepository>(DriverProfiles)
                .AddSingleton<IRefreshTokenRepository>(RefreshTokens)
                .AddSingleton<IUnitOfWork>(UnitOfWork)
                .AddSingleton<IPasswordHasher>(new FakeHasher())
                .AddSingleton<IJwtTokenService>(new FakeTokens())
                .AddSingleton<IEmailSender>(Email)
                .AddSingleton<ICurrentUserService>(new FakeCurrentUser(signedInAsAdmin ? Admin.Id : null))
                .AddSingleton<TimeProvider>(Time)
                .BuildServiceProvider()
                .GetRequiredService<ISender>();
        }
    }

    // ==========================================
    // 1. Get Users List Query (UC-ADM-04)
    // ==========================================

    [Fact]
    public async Task Admin_can_list_and_filter_users_with_pagination()
    {
        var app = new AdminUsersApp();

        var dt = new DataTable();
        dt.Columns.Add("Id", typeof(long));
        dt.Columns.Add("FullName", typeof(string));
        dt.Columns.Add("PhoneNumber", typeof(string));
        dt.Columns.Add("Email", typeof(string));
        dt.Columns.Add("Role", typeof(string));
        dt.Columns.Add("AccountStatus", typeof(string));
        dt.Columns.Add("EmailVerified", typeof(bool));
        dt.Columns.Add("CreatedAt", typeof(DateTimeOffset));

        dt.Rows.Add(2L, "Nguyễn Khách Hàng", "0911111111", "customer@modmove.vn", "Customer", "Active", true, TestApp.Start);

        app.Sql.Result = dt;
        app.Sql.ScalarResult = 1;

        var result = await app.Sender.Send(new GetUsersQuery(
            Page: 1,
            PageSize: 10,
            Search: "0911",
            Role: UserRole.Customer,
            Status: AccountStatus.Active));

        Assert.Equal(1, result.TotalCount);
        var item = Assert.Single(result.Items);
        Assert.Equal(2L, item.Id);
        Assert.Equal("Nguyễn Khách Hàng", item.FullName);
        Assert.Equal("Customer", item.Role);
        Assert.Equal("Active", item.AccountStatus);
        Assert.Equal("Customer", app.Sql.LastParameters["Role"]);
        Assert.Equal("Active", app.Sql.LastParameters["Status"]);
        Assert.Equal("%0911%", app.Sql.LastParameters["SearchPattern"]);
    }

    // ==========================================
    // 2. Get User Detail Query (UC-ADM-05)
    // ==========================================

    [Fact]
    public async Task Admin_can_get_user_detail_with_order_summary()
    {
        var app = new AdminUsersApp();

        var dt = new DataTable();
        dt.Columns.Add("Id", typeof(long));
        dt.Columns.Add("FullName", typeof(string));
        dt.Columns.Add("PhoneNumber", typeof(string));
        dt.Columns.Add("Email", typeof(string));
        dt.Columns.Add("Role", typeof(string));
        dt.Columns.Add("AccountStatus", typeof(string));
        dt.Columns.Add("EmailVerified", typeof(bool));
        dt.Columns.Add("CreatedAt", typeof(DateTimeOffset));
        dt.Columns.Add("UpdatedAt", typeof(DateTimeOffset));
        dt.Columns.Add("DriverApprovalStatus", typeof(string));
        dt.Columns.Add("DriverOnlineStatus", typeof(string));
        dt.Columns.Add("LicensePlate", typeof(string));
        dt.Columns.Add("TotalOrders", typeof(int));
        dt.Columns.Add("CompletedOrders", typeof(int));

        dt.Rows.Add(
            3L, "Trần Tài Xế", "0922222222", "driver@modmove.vn", "Driver", "Active", true,
            TestApp.Start, DBNull.Value, "Approved", "Online", "51C-888.99", 15, 12);

        app.Sql.Result = dt;

        var detail = await app.Sender.Send(new GetUserDetailQuery(3L));

        Assert.Equal(3L, detail.Id);
        Assert.Equal("Trần Tài Xế", detail.FullName);
        Assert.Equal("Driver", detail.Role);
        Assert.Equal("Approved", detail.DriverApprovalStatus);
        Assert.Equal("51C-888.99", detail.LicensePlate);
        Assert.Equal(15, detail.TotalOrders);
        Assert.Equal(12, detail.CompletedOrders);
    }

    [Fact]
    public async Task GetUserDetail_for_unknown_id_throws_NotFoundException()
    {
        var app = new AdminUsersApp();
        app.Sql.Result = new DataTable();

        await Assert.ThrowsAsync<NotFoundException>(() => app.Sender.Send(new GetUserDetailQuery(999L)));
    }

    // ==========================================
    // 3. Update User Account Status (UC-ADM-06)
    // ==========================================

    [Fact]
    public async Task Locking_account_sets_Locked_clears_fcm_and_revokes_all_refresh_tokens()
    {
        var app = new AdminUsersApp();
        var token1 = RefreshToken.Issue(app.Customer.Id, "hash-1", TestApp.Start);
        var token2 = RefreshToken.Issue(app.Customer.Id, "hash-2", TestApp.Start);
        var otherUserToken = RefreshToken.Issue(app.Driver.Id, "hash-driver", TestApp.Start);
        app.RefreshTokens.Add(token1);
        app.RefreshTokens.Add(token2);
        app.RefreshTokens.Add(otherUserToken);

        await app.Sender.Send(new UpdateUserStatusCommand(app.Customer.Id, AccountStatus.Locked, "Gian lận đơn hàng"));

        Assert.Equal(AccountStatus.Locked, app.Customer.AccountStatus);
        Assert.Null(app.Customer.FcmToken);
        Assert.NotNull(token1.RevokedAt);
        Assert.NotNull(token2.RevokedAt);
        Assert.Null(otherUserToken.RevokedAt); // untouched
        Assert.Equal(1, app.UnitOfWork.Saves);
        Assert.Single(app.Email.Sent);
    }

    [Fact]
    public async Task Locking_active_online_driver_sets_driver_offline()
    {
        var app = new AdminUsersApp();
        var profile = DriverProfile.Create(app.Driver.Id, "079090000002", "790000000002", "https://cdn/f.jpg", "https://cdn/b.jpg");
        var app1 = DriverApplication.Create(profile.Id, 1, 1L, "079090000002", "790000000002", "51C-222.22", 1000m,
            ["https://cdn/1.jpg", "https://cdn/2.jpg", "https://cdn/3.jpg", "https://cdn/4.jpg", "https://cdn/5.jpg"], TestApp.Start);
        profile.Submit(app1);
        app1.Approve(app.Admin.Id, TestApp.Start);
        profile.Approve(Vehicle.Create(profile.Id, 1L, "51C-222.22", 1000m, "250x160x160", "r", "i", "p"));
        profile.GoOnline();
        Assert.Equal(OnlineStatus.Online, profile.OnlineStatus);
        app.DriverProfiles.Add(profile);

        await app.Sender.Send(new UpdateUserStatusCommand(app.Driver.Id, AccountStatus.Locked, "Tạm khóa do khiếu nại"));

        Assert.Equal(AccountStatus.Locked, app.Driver.AccountStatus);
        Assert.Equal(OnlineStatus.Offline, profile.OnlineStatus);
    }

    [Fact]
    public async Task Deactivating_account_sets_Inactive_and_revokes_sessions()
    {
        var app = new AdminUsersApp();
        var token = RefreshToken.Issue(app.Driver.Id, "hash-drv", TestApp.Start);
        app.RefreshTokens.Add(token);

        await app.Sender.Send(new UpdateUserStatusCommand(app.Driver.Id, AccountStatus.Inactive, "Tài xế xin nghỉ hợp tác"));

        Assert.Equal(AccountStatus.Inactive, app.Driver.AccountStatus);
        Assert.Null(app.Driver.FcmToken);
        Assert.NotNull(token.RevokedAt);
    }

    [Fact]
    public async Task Unlocking_account_restores_Active_and_clears_temporary_login_lock()
    {
        var app = new AdminUsersApp();
        app.Customer.ChangeStatus(AccountStatus.Locked);
        for (var i = 0; i < 5; i++)
            app.Customer.RegisterFailedLogin(TestApp.Start);
        Assert.True(app.Customer.IsLocked(TestApp.Start));

        await app.Sender.Send(new UpdateUserStatusCommand(app.Customer.Id, AccountStatus.Active, "Đã xác minh và mở khóa"));

        Assert.Equal(AccountStatus.Active, app.Customer.AccountStatus);
        Assert.False(app.Customer.IsLocked(TestApp.Start));
        Assert.Equal(0, app.Customer.FailedLoginCount);
    }

    [Fact]
    public async Task Admin_cannot_change_own_account_status_MSG65()
    {
        var app = new AdminUsersApp();

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            app.Sender.Send(new UpdateUserStatusCommand(app.Admin.Id, AccountStatus.Locked, "Tự khóa mình")));

        Assert.Equal("CANNOT_CHANGE_OWN_STATUS", ex.Code);
        Assert.Equal(AccountStatus.Active, app.Admin.AccountStatus);
        Assert.Equal(0, app.UnitOfWork.Saves);
    }

    [Fact]
    public async Task UpdateUserStatus_missing_reason_fails_validation()
    {
        var app = new AdminUsersApp();

        await Assert.ThrowsAsync<ValidationException>(() =>
            app.Sender.Send(new UpdateUserStatusCommand(app.Customer.Id, AccountStatus.Locked, "   ")));
    }

    [Fact]
    public async Task UpdateUserStatus_unknown_user_throws_NotFoundException()
    {
        var app = new AdminUsersApp();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            app.Sender.Send(new UpdateUserStatusCommand(999L, AccountStatus.Locked, "Lý do hợp lệ")));
    }

    // ==========================================
    // 4. Create Dispatcher Account (UC-ADM-07)
    // ==========================================

    [Fact]
    public async Task Admin_can_create_dispatcher_account_and_dispatcher_can_sign_in_immediately()
    {
        var app = new AdminUsersApp();

        var dispatcherId = await app.Sender.Send(new CreateDispatcherCommand(
            "  Lê Điều Phối  ",
            "0988777666",
            "  Dispatcher.New@ModMove.vn ",
            "Disp@12345"));

        var created = await app.Users.GetByIdAsync(dispatcherId);
        Assert.NotNull(created);
        Assert.Equal("Lê Điều Phối", created.FullName);
        Assert.Equal("0988777666", created.PhoneNumber);
        Assert.Equal("dispatcher.new@modmove.vn", created.Email);
        Assert.Equal(UserRole.Dispatcher, created.Role);
        Assert.Equal(AccountStatus.Active, created.AccountStatus);
        Assert.True(created.EmailVerified); // No OTP step needed for internal Dispatcher (B7)
        Assert.Single(app.Email.Sent);

        // Verify the created dispatcher can sign in immediately with the issued password (UC-ADM-07 Step 7)
        var loginRes = await app.Sender.Send(new LoginCommand("dispatcher.new@modmove.vn", "Disp@12345"));
        Assert.Equal(dispatcherId, loginRes.UserId);
        Assert.Equal(UserRole.Dispatcher, loginRes.Role);
    }

    [Theory]
    [InlineData("0911111111", "unique@modmove.vn")]       // duplicate phone
    [InlineData("0977888999", "customer@modmove.vn")]     // duplicate email
    [InlineData("0977888999", "CUSTOMER@MODMOVE.VN")]     // duplicate email (case-insensitive)
    public async Task CreateDispatcher_with_duplicate_email_or_phone_throws_ConflictException_GB01(
        string phone, string email)
    {
        var app = new AdminUsersApp();

        await Assert.ThrowsAsync<ConflictException>(() =>
            app.Sender.Send(new CreateDispatcherCommand("Trùng Lặp", phone, email, "Disp@12345")));
    }
}
