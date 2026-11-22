using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ModMove.Application;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Features.MasterData.Coupons.Commands.UpdateCoupon;
using ModMove.Application.Features.Pricing.Commands.CreateVehicleType;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

/// <summary>UC-ADM-11/15: the Admin adds a vehicle type; UC-ADM-18: the Admin edits a coupon.</summary>
public class VehicleTypeAndCouponEditTests
{
    private sealed class AdminApp
    {
        public User Admin { get; } = User.Create("Admin", "0900000009", "admin@x.vn", "h:x", UserRole.Administrator, emailVerified: true);
        public InMemoryRepository<VehicleType> VehicleTypes { get; } = new();
        public InMemoryCoupons Coupons { get; } = new();
        public CountingUnitOfWork UnitOfWork { get; } = new();
        public ISender Sender { get; }

        public AdminApp()
        {
            var users = new InMemoryUsers(Admin);
            Sender = new ServiceCollection()
                .AddLogging()
                .AddApplication()
                .AddSingleton<ISqlConnectionFactory>(new FakeSql())
                .AddSingleton<IUserRepository>(users)
                .AddSingleton<IRepository<VehicleType>>(VehicleTypes)
                .AddSingleton<ICouponRepository>(Coupons)
                .AddSingleton<IUnitOfWork>(UnitOfWork)
                .AddSingleton<ICurrentUserService>(new FakeCurrentUser(Admin.Id))
                .AddSingleton<TimeProvider>(new MutableTime(TestApp.Start))
                .BuildServiceProvider()
                .GetRequiredService<ISender>();
        }
    }

    private static CreateVehicleTypeCommand Truck(string name = "  Xe tải 1 tấn ") =>
        new(name, 1000m, 310, 160, 160, 100_000m, 12_000m, 150_000m);

    [Fact]
    public async Task Admin_adds_an_active_vehicle_type_with_sizes_in_cm()
    {
        var app = new AdminApp();

        await app.Sender.Send(Truck());

        var added = Assert.Single(app.VehicleTypes.Items);
        Assert.Equal(("Xe tải 1 tấn", 1000m, 310, 160, 160), (added.Name, added.MaxLoadWeightKg, added.CargoBedLengthCm, added.CargoBedWidthCm, added.CargoBedHeightCm));
        Assert.Equal((100_000m, 12_000m, 150_000m), (added.BaseFare, added.PricePerKm, added.MinimumFare));
        Assert.True(added.IsActive);
        Assert.Equal(1, app.UnitOfWork.Saves);
    }

    [Theory]
    [InlineData("", 1000, 310, 100)]       // no name
    [InlineData("Xe", 0, 310, 100)]        // no load
    [InlineData("Xe", 1000, 0, 100)]       // no length
    [InlineData("Xe", 1000, 310, 0)]       // price per km must be > 0
    public async Task Invalid_vehicle_type_is_rejected(string name, decimal load, int lengthCm, decimal pricePerKm)
    {
        var app = new AdminApp();

        await Assert.ThrowsAsync<ValidationException>(() =>
            app.Sender.Send(new CreateVehicleTypeCommand(name, load, lengthCm, 100, 100, 0m, pricePerKm, 0m)));
        Assert.Empty(app.VehicleTypes.Items);
    }

    private static Coupon AddCoupon(AdminApp app, int? limit = 100)
    {
        var coupon = Coupon.Create("SALE50", 50_000, 100_000, TestApp.Start, TestApp.Start.AddDays(10), limit);
        TestIds.Set(coupon, 1);
        app.Coupons.Add(coupon);
        return coupon;
    }

    [Fact]
    public async Task Admin_edits_the_terms_and_the_code_stays()
    {
        var app = new AdminApp();
        var coupon = AddCoupon(app);

        await app.Sender.Send(new UpdateCouponCommand(1, 30_000, 80_000, TestApp.Start, TestApp.Start.AddDays(20), 200));

        Assert.Equal(("SALE50", 30_000m, 80_000m, TestApp.Start.AddDays(20), (int?)200),
            (coupon.DiscountCode, coupon.DiscountValue, coupon.MinimumOrderValue, coupon.ExpiresAt, coupon.UsageLimit));
        Assert.Equal(1, app.UnitOfWork.Saves);
    }

    [Fact]
    public async Task The_usage_limit_cannot_drop_below_the_uses_so_far()
    {
        var app = new AdminApp();
        var coupon = AddCoupon(app);
        coupon.MarkUsed();
        coupon.MarkUsed();

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            app.Sender.Send(new UpdateCouponCommand(1, 50_000, 100_000, TestApp.Start, TestApp.Start.AddDays(10), 1)));

        Assert.Equal("COUPON_LIMIT_BELOW_USED", error.Code);
        Assert.Equal(100, coupon.UsageLimit);
        Assert.Equal(0, app.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Unknown_coupon_is_not_found()
    {
        var app = new AdminApp();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            app.Sender.Send(new UpdateCouponCommand(99, 50_000, 100_000, TestApp.Start, TestApp.Start.AddDays(10), null)));
    }

    [Fact]
    public async Task Expiry_before_start_is_rejected()
    {
        var app = new AdminApp();
        AddCoupon(app);

        await Assert.ThrowsAsync<ValidationException>(() =>
            app.Sender.Send(new UpdateCouponCommand(1, 50_000, 100_000, TestApp.Start.AddDays(5), TestApp.Start, null)));
    }
}
