using ModMove.Application.Features.Matching;
using ModMove.Application.Features.Matching.Commands.AcceptOrder;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.UnitTests.Domain;

/// <summary>Unit tests for Driver Matching domain rules (B5, GB-08..10, GB-24/31, DECISIONS 3.2) and validators.</summary>
public class ShippingOrderMatchingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);

    private static ShippingOrder CreateOrder(PaymentMethod method)
    {
        var order = ShippingOrder.Create(
            "MM20261103001",
            1,
            1,
            null,
            "100 Lê Lợi",
            10.7756m,
            106.7019m,
            "200 Nguyễn Huệ",
            10.7856m,
            106.7119m,
            "Nguyễn Văn Nhận",
            "0909123456",
            5m,
            20,
            false,
            100_000m,
            0m,
            0m,
            method,
            method == PaymentMethod.Cash ? PayerRole.Sender : null,
            CargoDetail.Create("GENERAL", 1, 10m, 50, 40, 30, ["https://cdn.example.com/1.jpg"], null),
            Now);
        order.CreatedAt = Now;
        return order;
    }

    [Fact]
    public void Pending_Cash_order_can_be_accepted_and_increments_RowVersion()
    {
        var order = CreateOrder(PaymentMethod.Cash);
        var initialVersion = order.RowVersion;
        var acceptedAt = Now.AddMinutes(1);

        order.Accept(driverProfileId: 42, driverUserId: 100, acceptedAt);

        Assert.Equal(OrderStatus.Accepted, order.OrderStatus);
        Assert.Equal(42, order.DriverProfileId);
        Assert.Equal(acceptedAt, order.AcceptedAt);
        Assert.Equal(initialVersion + 1, order.RowVersion);
    }

    [Fact]
    public void Paid_PayOS_order_can_be_accepted()
    {
        var order = CreateOrder(PaymentMethod.PayOS);
        order.MarkPaid(Now.AddMinutes(1));

        order.Accept(driverProfileId: 42, driverUserId: 100, Now.AddMinutes(2));

        Assert.Equal(OrderStatus.Accepted, order.OrderStatus);
        Assert.Equal(42, order.DriverProfileId);
    }

    [Fact]
    public void Cancelled_order_cannot_be_accepted()
    {
        var order = CreateOrder(PaymentMethod.Cash);
        order.CancelByCustomer(customerId: 1, "Đổi ý", Now.AddMinutes(1));

        Assert.Throws<DomainException>(() =>
            order.Accept(driverProfileId: 42, driverUserId: 100, Now.AddMinutes(2)));
    }

    [Fact]
    public void Already_accepted_order_throws_DomainException_when_accepted_again_GB08()
    {
        var order = CreateOrder(PaymentMethod.Cash);
        order.Accept(driverProfileId: 42, driverUserId: 100, Now.AddMinutes(1));

        Assert.Throws<DomainException>(() => order.Accept(driverProfileId: 99, driverUserId: 200, Now.AddMinutes(2)));
        Assert.Equal(42, order.DriverProfileId);
    }

    [Theory]
    [InlineData(599, false)]
    [InlineData(600, true)]
    [InlineData(601, true)]
    public void Cash_order_IsMatchingExpired_measures_from_CreatedAt_GB24(int elapsedSeconds, bool expectedExpired)
    {
        var order = CreateOrder(PaymentMethod.Cash);

        Assert.Equal(expectedExpired, order.IsMatchingExpired(Now.AddSeconds(elapsedSeconds)));
    }

    [Fact]
    public void Non_pending_order_is_never_considered_matching_expired()
    {
        var order = CreateOrder(PaymentMethod.Cash);
        order.Accept(driverProfileId: 42, driverUserId: 100, Now.AddMinutes(1));

        // Even after matching timeout (e.g. 15 minutes later), once accepted it is no longer expired for matching
        Assert.False(order.IsMatchingExpired(Now.AddMinutes(15)));
    }

    [Fact]
    public void PayOS_order_IsMatchingExpired_is_false_while_unpaid_and_measures_from_PaidAt_once_paid_DECISIONS_3_2()
    {
        var order = CreateOrder(PaymentMethod.PayOS);

        // Even 30 minutes after CreatedAt, an unpaid PayOS order is NOT expired for matching (JOB-PAY-01 handles unpaid expiry)
        Assert.False(order.IsMatchingExpired(Now.AddMinutes(30)));

        var paidAt = Now.AddMinutes(5);
        order.MarkPaid(paidAt);

        Assert.False(order.IsMatchingExpired(paidAt.AddSeconds(599)));
        Assert.True(order.IsMatchingExpired(paidAt.AddSeconds(600)));
    }

    [Theory]
    [InlineData(1799, false)]
    [InlineData(1800, true)]
    [InlineData(1801, true)]
    public void Unpaid_PayOS_order_IsPaymentExpired_measures_30_minutes_from_CreatedAt_DECISIONS_3_1(int elapsedSeconds, bool expectedExpired)
    {
        var order = CreateOrder(PaymentMethod.PayOS);

        Assert.Equal(expectedExpired, order.IsPaymentExpired(Now.AddSeconds(elapsedSeconds)));
    }

    [Fact]
    public void Paid_PayOS_order_cash_order_and_cancelled_order_are_never_payment_expired()
    {
        var paidPayOs = CreateOrder(PaymentMethod.PayOS);
        paidPayOs.MarkPaid(Now.AddMinutes(5));

        var cash = CreateOrder(PaymentMethod.Cash);

        var cancelledPayOs = CreateOrder(PaymentMethod.PayOS);
        cancelledPayOs.CancelByCustomer(customerId: 1, "Đổi ý", Now.AddMinutes(2));

        Assert.False(paidPayOs.IsPaymentExpired(Now.AddMinutes(35)));
        Assert.False(cash.IsPaymentExpired(Now.AddMinutes(35)));
        Assert.False(cancelledPayOs.IsPaymentExpired(Now.AddMinutes(35)));
    }

    [Fact]
    public void GeoDistance_HaversineKm_returns_zero_for_same_coordinates_and_accurate_distance_for_nearby_points()
    {
        Assert.Equal(0d, GeoDistance.HaversineKm(10.7756m, 106.7019m, 10.7756m, 106.7019m), precision: 6);

        var distanceKm = GeoDistance.HaversineKm(10.7756m, 106.7019m, 10.7856m, 106.7119m);
        Assert.InRange(distanceKm, 1.50d, 1.60d);
    }

    [Fact]
    public void GeoDistance_HaversineKm_handles_negative_coordinates_and_global_distances()
    {
        // Sydney, Australia (-33.8688, 151.2093) to Rio de Janeiro, Brazil (-22.9068, -43.1729)
        var distanceKm = GeoDistance.HaversineKm(-33.8688m, 151.2093m, -22.9068m, -43.1729m);
        Assert.InRange(distanceKm, 13_000d, 14_000d);

        // North Pole (90, 0) to South Pole (-90, 0) ~ 20,015 km (half of Earth circumference)
        var poleDistanceKm = GeoDistance.HaversineKm(90m, 0m, -90m, 0m);
        Assert.InRange(poleDistanceKm, 20_000d, 20_050d);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(long.MaxValue, true)]
    [InlineData(0, false)]
    [InlineData(-5, false)]
    [InlineData(long.MinValue, false)]
    public void AcceptOrderCommandValidator_requires_positive_OrderId(long orderId, bool expectedValid)
    {
        var validator = new AcceptOrderCommandValidator();
        var result = validator.Validate(new AcceptOrderCommand(orderId));

        Assert.Equal(expectedValid, result.IsValid);
    }
}
