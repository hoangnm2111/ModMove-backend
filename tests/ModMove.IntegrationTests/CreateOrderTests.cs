using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Orders.Commands.CreateOrder;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

/// <summary>POST api/customer/orders: cash and PayOS orders (GST-17, B10, B11, 4.3) through the real pipeline with fakes.</summary>
public class CreateOrderTests
{
    private const string Photo = "https://res.cloudinary.com/demo/image/upload/modmove/customers/1/a.jpg";

    private static TestApp App()
    {
        var customer = User.Create("An", "0900000001", "an@x.vn", "h:secret123", UserRole.Customer, emailVerified: true);
        var app = TestApp.SignedInAs(customer);
        app.VehicleTypes.Add(VehicleType.Create("Xe van 500kg", 500, 170, 120, 120, 50_000, 10_000, 80_000));
        app.Settings.Add(PlatformSetting.Create(SettingKeys.LoadingFee, "100000", TestApp.Start));
        app.Coupons.Add(Coupon.Create("WELCOME20K", 20_000, 100_000, TestApp.Start.AddDays(-1), TestApp.Start.AddYears(1), 1_000));
        app.Maps.Route = new RouteInfo(12m, 30);
        return app;
    }

    private static CreateOrderCommand Order(
        PaymentMethod method = PaymentMethod.Cash, PayerRole? payer = PayerRole.Sender, bool commitment = true,
        IReadOnlyList<string>? photos = null) =>
        new(1, "Hoàn Kiếm, Hà Nội", "Cầu Giấy, Hà Nội", "Bình", "0912345678",
            new CargoInput("FURNITURE", 2, 100, 100, 100, 100, photos ?? [Photo], "Dễ vỡ"),
            HasLoadingService: true, CouponCode: "WELCOME20K", method, payer, commitment);

    [Fact]
    public async Task Cash_order_is_created_placeorder_unpaid_B10()
    {
        var app = App();

        var result = await app.Sender.Send(Order());

        var order = app.Orders.Items.Single();
        Assert.Equal(1, app.UnitOfWork.Saves);
        Assert.Equal(OrderStatus.PlaceOrder, order.OrderStatus);
        Assert.Equal(PaymentStatus.Unpaid, order.PaymentStatus);
        Assert.Equal("MM20261020001", result.OrderCode);
        Assert.Equal(250_000m, result.TotalFee);
        Assert.Equal(1, order.CustomerId);
        Assert.Equal(TestApp.Start, order.WarrantyAcceptedAt);
        Assert.Equal("FURNITURE", order.Cargo!.CargoType);
    }

    [Fact]
    public async Task Coupon_use_is_counted_on_create_B11()
    {
        var app = App();

        await app.Sender.Send(Order());

        Assert.Equal(1, app.Coupons.Items[0].UsedCount);
    }

    [Fact]
    public async Task PayOS_order_is_created_placeorder_unpaid_B10()
    {
        var app = App();

        await app.Sender.Send(Order(method: PaymentMethod.PayOS, payer: null));

        var order = app.Orders.Items.Single();
        Assert.Equal((PaymentMethod.PayOS, (PayerRole?)null), (order.PaymentMethod, order.PayerRole));
        Assert.Equal((OrderStatus.PlaceOrder, PaymentStatus.Unpaid), (order.OrderStatus, order.PaymentStatus));
        Assert.Equal(1, app.UnitOfWork.Saves);
    }

    [Fact]
    public async Task PayOS_order_with_a_payer_is_rejected() =>
        await Assert.ThrowsAsync<ValidationException>(() => App().Sender.Send(Order(method: PaymentMethod.PayOS, payer: PayerRole.Sender)));

    [Fact]
    public async Task Unknown_payment_method_is_rejected() =>
        await Assert.ThrowsAsync<ValidationException>(() => App().Sender.Send(Order(method: (PaymentMethod)9)));

    [Fact]
    public async Task Matching_expected_total_creates_the_order_GST17()
    {
        var app = App();

        var result = await app.Sender.Send(Order() with { ExpectedTotalFee = 250_000m });

        Assert.Equal(250_000m, result.TotalFee);
        Assert.Single(app.Orders.Items);
    }

    [Fact]
    public async Task Changed_total_is_FARE_CHANGED_and_coupon_not_used_GST17()
    {
        var app = App();

        var error = await Assert.ThrowsAsync<DomainException>(() => app.Sender.Send(Order() with { ExpectedTotalFee = 240_000m }));

        Assert.Equal("FARE_CHANGED", error.Code);
        Assert.Contains("250000", error.Message);
        Assert.Empty(app.Orders.Items);
        Assert.Equal(0, app.Coupons.Items[0].UsedCount);
        Assert.Equal(0, app.UnitOfWork.Saves);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1000.5)]
    public async Task Expected_total_is_a_whole_non_negative_amount(double amount) =>
        await Assert.ThrowsAsync<ValidationException>(() => App().Sender.Send(Order() with { ExpectedTotalFee = (decimal)amount }));

    [Fact]
    public async Task Cargo_type_must_be_a_code_from_the_list()
    {
        var app = App();
        var order = Order();

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            app.Sender.Send(order with { Cargo = order.Cargo with { CargoType = "Tủ lạnh" } }));

        Assert.Contains("Cargo.CargoType", error.Errors.Keys);
        Assert.Empty(app.Orders.Items);
        Assert.Equal(0, app.Coupons.Items[0].UsedCount);
    }

    [Fact]
    public async Task Cargo_type_switched_off_by_the_admin_is_refused()
    {
        var app = App();
        app.MasterData.Items.Single(i => i.Code == "FURNITURE").SetActive(false);

        await Assert.ThrowsAsync<ValidationException>(() => app.Sender.Send(Order()));

        Assert.Empty(app.Orders.Items);
    }

    [Fact]
    public async Task Missing_prohibited_goods_commitment_is_rejected_4_3() =>
        await Assert.ThrowsAsync<ValidationException>(() => App().Sender.Send(Order(commitment: false)));

    [Fact]
    public async Task Cash_without_payer_is_rejected() =>
        await Assert.ThrowsAsync<ValidationException>(() => App().Sender.Send(Order(payer: null)));

    [Fact]
    public async Task Photo_outside_our_cloudinary_is_rejected() =>
        await Assert.ThrowsAsync<ValidationException>(() => App().Sender.Send(Order(photos: ["https://example.com/a.jpg"])));

    [Fact]
    public async Task Six_photos_are_rejected_GB06() =>
        await Assert.ThrowsAsync<ValidationException>(() => App().Sender.Send(Order(photos: Enumerable.Repeat(Photo, 6).ToList())));

    [Fact]
    public async Task Missing_cargo_is_a_validation_error() =>
        await Assert.ThrowsAsync<ValidationException>(() => App().Sender.Send(Order() with { Cargo = null! }));

    [Fact]
    public async Task Second_order_today_gets_the_next_number()
    {
        var app = App();
        await app.Sender.Send(Order());
        app.Orders.Items[0].CreatedAt = app.Time.Now; // the database stamps this on save

        var second = await app.Sender.Send(Order());

        Assert.Equal("MM20261020002", second.OrderCode);
    }

    [Fact]
    public async Task Duplicate_code_race_surfaces_as_conflict()
    {
        var app = App();
        app.UnitOfWork.ThrowConflictOnSave = true;

        await Assert.ThrowsAsync<ConflictException>(() => app.Sender.Send(Order()));
    }

    [Fact]
    public async Task Locked_account_cannot_order()
    {
        var app = App();
        app.Users.Items[0].ChangeStatus(AccountStatus.Locked);

        var error = await Assert.ThrowsAsync<ForbiddenAccessException>(() => app.Sender.Send(Order()));

        Assert.Equal("ACCOUNT_INACTIVE", error.Code);
    }
}
