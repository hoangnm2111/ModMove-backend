using System.Data;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ModMove.Application;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Features.MasterData.Commands.CreateMasterDataItem;
using ModMove.Application.Features.MasterData.Commands.SetMasterDataStatus;
using ModMove.Application.Features.MasterData.Commands.UpdateMasterDataItem;
using ModMove.Application.Features.MasterData.Coupons.Commands.CreateCoupon;
using ModMove.Application.Features.MasterData.Coupons.Commands.SetCouponStatus;
using ModMove.Application.Features.MasterData.Coupons.Queries.GetCoupons;
using ModMove.Application.Features.MasterData.Queries.GetMasterDataList;
using ModMove.Application.Features.Pricing.Commands.SetVehicleTypeStatus;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.IntegrationTests;

/// <summary>Screen #36: UC-ADM-14..16, UC-ADM-18 (Master Data, Vehicle Types status &amp; Coupons).</summary>
public class MasterDataAndCouponTests
{
    private sealed class AdminTestApp
    {
        public User Admin { get; } = User.Create("Admin", "0900000009", "admin@x.vn", "h:x", UserRole.Administrator, emailVerified: true);
        public FakeSql Sql { get; } = new();
        public InMemoryRepository<MasterDataItem> MasterData { get; } = new();
        public InMemoryRepository<VehicleType> VehicleTypes { get; } = new();
        public InMemoryCoupons Coupons { get; } = new();
        public CountingUnitOfWork UnitOfWork { get; } = new();
        public MutableTime Time { get; } = new(TestApp.Start);
        public ISender Sender { get; }

        public AdminTestApp(bool signedIn = true)
        {
            var users = new InMemoryUsers(Admin);

            Sender = new ServiceCollection()
                .AddLogging()
                .AddApplication()
                .AddSingleton<ISqlConnectionFactory>(Sql)
                .AddSingleton<IUserRepository>(users)
                .AddSingleton<IRepository<MasterDataItem>>(MasterData)
                .AddSingleton<IRepository<VehicleType>>(VehicleTypes)
                .AddSingleton<ICouponRepository>(Coupons)
                .AddSingleton<IUnitOfWork>(UnitOfWork)
                .AddSingleton<ICurrentUserService>(new FakeCurrentUser(signedIn ? Admin.Id : null))
                .AddSingleton<TimeProvider>(Time)
                .BuildServiceProvider()
                .GetRequiredService<ISender>();
        }
    }

    // ==========================================
    // 1. Master Data Tests (UC-ADM-14, 15, 16)
    // ==========================================

    [Fact]
    public async Task Admin_can_query_master_data_items_filtered_by_category()
    {
        var app = new AdminTestApp();
        var dt = new DataTable();
        dt.Columns.Add("Id", typeof(long));
        dt.Columns.Add("Category", typeof(string));
        dt.Columns.Add("Code", typeof(string));
        dt.Columns.Add("Name", typeof(string));
        dt.Columns.Add("Description", typeof(string));
        dt.Columns.Add("IsActive", typeof(bool));
        dt.Columns.Add("CreatedAt", typeof(DateTimeOffset));
        dt.Columns.Add("UpdatedAt", typeof(DateTimeOffset));

        dt.Rows.Add(1L, "POLICY", "TERMS", "Điều khoản", "Nội dung...", true, TestApp.Start, null);
        dt.Rows.Add(2L, "POLICY", "PRIVACY", "Bảo mật", "Chính sách...", true, TestApp.Start, null);
        app.Sql.Result = dt;

        var result = await app.Sender.Send(new GetMasterDataListQuery("POLICY"));

        Assert.Equal(2, result.Count);
        Assert.Equal("TERMS", result[0].Code);
        Assert.Contains("WHERE category = @Category", app.Sql.LastSql);
        Assert.Equal("POLICY", app.Sql.LastParameters["Category"]);
    }

    [Fact]
    public async Task Admin_can_query_all_master_data_without_category()
    {
        var app = new AdminTestApp();
        var dt = new DataTable();
        dt.Columns.Add("Id", typeof(long));
        dt.Columns.Add("Category", typeof(string));
        dt.Columns.Add("Code", typeof(string));
        dt.Columns.Add("Name", typeof(string));
        dt.Columns.Add("Description", typeof(string));
        dt.Columns.Add("IsActive", typeof(bool));
        dt.Columns.Add("CreatedAt", typeof(DateTimeOffset));
        dt.Columns.Add("UpdatedAt", typeof(DateTimeOffset));
        app.Sql.Result = dt;

        await app.Sender.Send(new GetMasterDataListQuery());

        Assert.Contains("FROM master_data_items", app.Sql.LastSql);
        Assert.DoesNotContain("WHERE category", app.Sql.LastSql);
    }

    [Fact]
    public async Task Admin_can_create_master_data_item()
    {
        var app = new AdminTestApp();
        app.Sql.ScalarResult = 0; // duplicate count = 0

        var command = new CreateMasterDataItemCommand("POLICY", "WARRANTY", "Chính sách bảo hành", "Nội dung bảo hành");
        var id = await app.Sender.Send(command);

        Assert.True(id > 0);
        Assert.Single(app.MasterData.Items);
        var item = app.MasterData.Items[0];
        Assert.Equal("POLICY", item.Category);
        Assert.Equal("WARRANTY", item.Code);
        Assert.Equal("Chính sách bảo hành", item.Name);
        Assert.Equal("Nội dung bảo hành", item.Description);
        Assert.True(item.IsActive);
        Assert.Equal(1, app.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Creating_duplicate_master_data_code_in_same_category_throws_conflict()
    {
        var app = new AdminTestApp();
        app.Sql.ScalarResult = 1; // duplicate found in DB

        var command = new CreateMasterDataItemCommand("POLICY", "TERMS", "Điều khoản", null);

        await Assert.ThrowsAsync<ConflictException>(() => app.Sender.Send(command));
        Assert.Empty(app.MasterData.Items);
        Assert.Equal(0, app.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Admin_can_update_master_data_name_and_description()
    {
        var app = new AdminTestApp();
        var item = MasterDataItem.Create("POLICY", "TERMS", "Tên cũ", "Mô tả cũ");
        TestIds.Set(item, 10);
        app.MasterData.Add(item);

        await app.Sender.Send(new UpdateMasterDataItemCommand(10, "Tên mới", "Mô tả mới đã chỉnh sửa"));

        Assert.Equal("Tên mới", item.Name);
        Assert.Equal("Mô tả mới đã chỉnh sửa", item.Description);
        Assert.Equal(1, app.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Updating_non_existent_master_data_item_throws_not_found()
    {
        var app = new AdminTestApp();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            app.Sender.Send(new UpdateMasterDataItemCommand(999, "Tên", null)));
        Assert.Equal(0, app.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Admin_can_toggle_master_data_status()
    {
        var app = new AdminTestApp();
        var item = MasterDataItem.Create("CARGO_TYPE", "FRAGILE", "Hàng dễ vỡ");
        TestIds.Set(item, 1);
        app.MasterData.Add(item);

        // Deactivate
        await app.Sender.Send(new SetMasterDataStatusCommand(1, false));
        Assert.False(item.IsActive);
        Assert.Equal(1, app.UnitOfWork.Saves);

        // Reactivate
        await app.Sender.Send(new SetMasterDataStatusCommand(1, true));
        Assert.True(item.IsActive);
        Assert.Equal(2, app.UnitOfWork.Saves);
    }

    // ==========================================
    // 2. VehicleType Status Tests (UC-ADM-11)
    // ==========================================

    [Fact]
    public async Task Admin_can_toggle_vehicle_type_status()
    {
        var app = new AdminTestApp();
        var van = VehicleType.Create("Xe van 500kg", 500, 170, 120, 120, 50_000, 10_000, 80_000);
        TestIds.Set(van, 1);
        app.VehicleTypes.Add(van);

        // Disable vehicle type
        await app.Sender.Send(new SetVehicleTypeStatusCommand(1, false));
        Assert.False(van.IsActive);
        Assert.Equal(1, app.UnitOfWork.Saves);

        // Re-enable vehicle type
        await app.Sender.Send(new SetVehicleTypeStatusCommand(1, true));
        Assert.True(van.IsActive);
        Assert.Equal(2, app.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Setting_status_for_unknown_vehicle_type_throws_not_found()
    {
        var app = new AdminTestApp();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            app.Sender.Send(new SetVehicleTypeStatusCommand(999, false)));
    }

    // ==========================================
    // 3. Coupons Tests (GB-21, UC-ADM-18)
    // ==========================================

    [Fact]
    public async Task Admin_can_query_coupons_paginated()
    {
        var app = new AdminTestApp();
        var dt = new DataTable();
        dt.Columns.Add("Id", typeof(long));
        dt.Columns.Add("DiscountCode", typeof(string));
        dt.Columns.Add("DiscountValue", typeof(decimal));
        dt.Columns.Add("MinimumOrderValue", typeof(decimal));
        dt.Columns.Add("ValidFrom", typeof(DateTimeOffset));
        dt.Columns.Add("ExpiresAt", typeof(DateTimeOffset));
        dt.Columns.Add("UsageLimit", typeof(int));
        dt.Columns.Add("UsedCount", typeof(int));
        dt.Columns.Add("IsActive", typeof(bool));
        dt.Columns.Add("CreatedAt", typeof(DateTimeOffset));

        dt.Rows.Add(1L, "CHAOBANMOI", 20_000m, 100_000m, TestApp.Start, TestApp.Start.AddMonths(1), 100, 5, true, TestApp.Start);
        app.Sql.Result = dt;
        app.Sql.ScalarResult = 1; // totalCount = 1

        var response = await app.Sender.Send(new GetCouponsQuery(Page: 1, PageSize: 10, IsActive: true));

        Assert.Equal(1, response.TotalCount);
        Assert.Single(response.Items);
        Assert.Equal("CHAOBANMOI", response.Items[0].DiscountCode);
        Assert.Equal(20_000m, response.Items[0].DiscountValue);
        Assert.Contains("FROM coupons", app.Sql.LastSql);
    }

    [Fact]
    public async Task Admin_can_create_new_coupon()
    {
        var app = new AdminTestApp();
        var from = TestApp.Start;
        var to = TestApp.Start.AddDays(30);

        var command = new CreateCouponCommand("modmove2026", 50_000m, 150_000m, from, to, 500);
        var id = await app.Sender.Send(command);

        Assert.True(id > 0);
        Assert.Single(app.Coupons.Items);
        var coupon = app.Coupons.Items[0];
        Assert.Equal("MODMOVE2026", coupon.DiscountCode); // normalized to uppercase
        Assert.Equal(50_000m, coupon.DiscountValue);
        Assert.Equal(150_000m, coupon.MinimumOrderValue);
        Assert.Equal(500, coupon.UsageLimit);
        Assert.Equal(0, coupon.UsedCount);
        Assert.True(coupon.IsActive);
        Assert.Equal(1, app.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Creating_duplicate_coupon_code_throws_conflict()
    {
        var app = new AdminTestApp();
        var existing = Coupon.Create("SALE50", 50_000, 100_000, TestApp.Start, TestApp.Start.AddDays(10), null);
        TestIds.Set(existing, 1);
        app.Coupons.Add(existing);

        var command = new CreateCouponCommand("sale50", 30_000, 50_000, TestApp.Start, TestApp.Start.AddDays(5), null);

        await Assert.ThrowsAsync<ConflictException>(() => app.Sender.Send(command));
        Assert.Single(app.Coupons.Items);
        Assert.Equal(0, app.UnitOfWork.Saves);
    }

    [Theory]
    [InlineData("", 50_000, 100_000, 1, 30, null)]        // empty code
    [InlineData("CODE@#", 50_000, 100_000, 1, 30, null)]    // special characters in code
    [InlineData("SALE", 0, 100_000, 1, 30, null)]          // discountValue = 0
    [InlineData("SALE", -10_000, 100_000, 1, 30, null)]    // negative discountValue
    [InlineData("SALE", 50_000, -1, 1, 30, null)]          // negative min order
    [InlineData("SALE", 50_000, 100_000, 30, 1, null)]     // expiresAt before validFrom
    [InlineData("SALE", 50_000, 100_000, 1, 30, 0)]        // usageLimit = 0
    [InlineData("SALE", 50_000, 100_000, 1, 30, -5)]       // negative usageLimit
    public async Task Invalid_coupon_data_is_rejected_by_validator(
        string code, decimal discountValue, decimal minOrder, int fromDays, int toDays, int? usageLimit)
    {
        var app = new AdminTestApp();
        var command = new CreateCouponCommand(
            code, discountValue, minOrder,
            TestApp.Start.AddDays(fromDays), TestApp.Start.AddDays(toDays), usageLimit);

        await Assert.ThrowsAsync<ValidationException>(() => app.Sender.Send(command));
        Assert.Empty(app.Coupons.Items);
        Assert.Equal(0, app.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Admin_can_toggle_coupon_status()
    {
        var app = new AdminTestApp();
        var coupon = Coupon.Create("FLASH10", 10_000, 50_000, TestApp.Start, TestApp.Start.AddDays(5), null);
        TestIds.Set(coupon, 1);
        app.Coupons.Add(coupon);

        // Deactivate
        await app.Sender.Send(new SetCouponStatusCommand(1, false));
        Assert.False(coupon.IsActive);
        Assert.Equal(1, app.UnitOfWork.Saves);

        // Reactivate
        await app.Sender.Send(new SetCouponStatusCommand(1, true));
        Assert.True(coupon.IsActive);
        Assert.Equal(2, app.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Setting_status_of_non_existent_coupon_throws_not_found()
    {
        var app = new AdminTestApp();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            app.Sender.Send(new SetCouponStatusCommand(999, false)));
    }
}
