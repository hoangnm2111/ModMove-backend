using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Trips.Commands.CollectCash;
using ModMove.Application.Features.Trips.Commands.ConfirmArrival;
using ModMove.Application.Features.Trips.Commands.ConfirmPickup;
using ModMove.Application.Features.Trips.Commands.DeliverTrip;
using ModMove.Application.Features.Trips.Commands.NotifyArrivalAtDropoff;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

/// <summary>
/// DECISIONS 7.6: while the driver who holds the order has an Open or InProgress incident, every next trip step is
/// refused with INCIDENT_PENDING; a customer's incident, or one left by a previous driver, does not hold the trip.
/// </summary>
public class TripIncidentHoldTests
{
    private const string Photo = "https://res.cloudinary.com/demo/image/upload/modmove/drivers/1/1.jpg";
    private static readonly DateTimeOffset Now = TestApp.Start;

    public enum Step { Arrive, Pickup, ArriveAtDropoff, CollectCash, Deliver }

    private sealed class Setup
    {
        public TestApp App { get; }
        public User Driver { get; }
        public User PreviousDriver { get; }
        public User Customer { get; }
        public DriverProfile Profile { get; }
        public DriverProfile PreviousProfile { get; }

        public Setup()
        {
            Driver = User.Create("Tài", "0912000001", "driver@x.vn", "h:x", UserRole.Driver, emailVerified: true);
            PreviousDriver = User.Create("Cũ", "0912000002", "old@x.vn", "h:x", UserRole.Driver, emailVerified: true);
            Customer = User.Create("Khách", "0909000001", "customer@x.vn", "h:x", UserRole.Customer, emailVerified: true);
            App = TestApp.SignedInAs(Driver, PreviousDriver, Customer);

            Profile = DriverProfile.Create(Driver.Id, Photo, Photo);
            App.DriverProfiles.Add(Profile);
            Profile.Approve(Vehicle.Create(Profile.Id, 1, "51C-123.45", 1000, "200x150x150", Photo, Photo, Photo));

            PreviousProfile = DriverProfile.Create(PreviousDriver.Id, Photo, Photo);
            App.DriverProfiles.Add(PreviousProfile);

            App.Settings.Add(PlatformSetting.Create(SettingKeys.CommissionRate, "0.10", Now));
        }

        /// <summary>An order of the driver walked to the state right before <paramref name="step"/>.</summary>
        public ShippingOrder OrderBefore(Step step)
        {
            var method = step == Step.CollectCash ? PaymentMethod.Cash : PaymentMethod.PayOS;
            var order = ShippingOrder.Create(
                "MM20261108001", Customer.Id, vehicleTypeId: 1, couponId: null,
                "A", 10.77m, 106.70m, "B", 10.84m, 106.78m, "Nhận", "0988777666", 12.5m, 35, hasLoadingService: false,
                distanceFee: 200_000m, serviceFee: 0m, discountAmount: 0m,
                method, method == PaymentMethod.Cash ? PayerRole.Sender : null,
                CargoDetail.Create("FURNITURE", 1, 50, 100, 50, 50, [Photo], null), Now);
            if (method == PaymentMethod.PayOS)
                order.MarkPaid(Now);
            order.Accept(Profile.Id, Driver.Id, Now);
            if (step != Step.Arrive)
                order.ConfirmArrival(Driver.Id, Now);
            if (step is Step.ArriveAtDropoff or Step.Deliver)
            {
                order.ConfirmPickup([Photo, Photo], Driver.Id, Now);
                order.ApprovePickupEvidence(Customer.Id, Now);
            }
            App.Orders.Add(order);
            return order;
        }

        public InTransitIncident IncidentBy(User reporter, ShippingOrder order)
        {
            var incident = InTransitIncident.Report(order.Id, reporter.Id, IncidentType.VehicleBreakdown, "Xe hỏng", null, null, null);
            App.Incidents.Add(incident);
            return incident;
        }

        public Task Run(Step step, ShippingOrder order) => step switch
        {
            Step.Arrive => App.Sender.Send(new ConfirmArrivalCommand(order.Id)),
            Step.Pickup => App.Sender.Send(new ConfirmPickupCommand(order.Id, [Photo, Photo])),
            Step.ArriveAtDropoff => App.Sender.Send(new NotifyArrivalAtDropoffCommand(order.Id)),
            Step.CollectCash => App.Sender.Send(new CollectCashCommand(order.Id, order.PickupLatitude, order.PickupLongitude)),
            Step.Deliver => App.Sender.Send(new DeliverTripCommand(order.Id, Photo, CashCollected: false)),
            _ => throw new ArgumentOutOfRangeException(nameof(step)),
        };
    }

    public static TheoryData<Step> Steps => new() { Step.Arrive, Step.Pickup, Step.ArriveAtDropoff, Step.CollectCash, Step.Deliver };

    [Theory]
    [MemberData(nameof(Steps))]
    public async Task Open_incident_of_the_driver_holds_the_next_step_7_6(Step step)
    {
        var s = new Setup();
        var order = s.OrderBefore(step);
        var statusBefore = order.OrderStatus;
        s.IncidentBy(s.Driver, order);

        var error = await Assert.ThrowsAsync<DomainException>(() => s.Run(step, order));

        Assert.Equal("INCIDENT_PENDING", error.Code);
        Assert.Equal(statusBefore, order.OrderStatus);
        Assert.Equal(0, s.App.UnitOfWork.Saves);
    }

    [Theory]
    [MemberData(nameof(Steps))]
    public async Task Incident_being_handled_still_holds_the_next_step_7_6(Step step)
    {
        var s = new Setup();
        var order = s.OrderBefore(step);
        s.IncidentBy(s.Driver, order).StartHandling(dispatcherId: 99);

        var error = await Assert.ThrowsAsync<DomainException>(() => s.Run(step, order));

        Assert.Equal("INCIDENT_PENDING", error.Code);
    }

    [Theory]
    [MemberData(nameof(Steps))]
    public async Task Continue_decision_releases_the_trip_7_6(Step step)
    {
        var s = new Setup();
        var order = s.OrderBefore(step);
        s.IncidentBy(s.Driver, order).Resolve(IncidentDecision.ContinueTrip, "Đi tiếp", 99, Now);

        await s.Run(step, order);
    }

    [Theory]
    [MemberData(nameof(Steps))]
    public async Task Dismissed_report_releases_the_trip_7_6(Step step)
    {
        var s = new Setup();
        var order = s.OrderBefore(step);
        s.IncidentBy(s.Driver, order).CancelIncident("Báo nhầm", 99, Now);

        await s.Run(step, order);
    }

    [Theory]
    [MemberData(nameof(Steps))]
    public async Task Customer_incident_does_not_hold_the_trip_7_6(Step step)
    {
        var s = new Setup();
        var order = s.OrderBefore(step);
        s.IncidentBy(s.Customer, order);

        await s.Run(step, order);
    }

    [Theory]
    [MemberData(nameof(Steps))]
    public async Task Open_incident_left_by_a_previous_driver_does_not_hold_the_new_driver_7_6(Step step)
    {
        var s = new Setup();
        var order = s.OrderBefore(step);
        s.IncidentBy(s.PreviousDriver, order);

        await s.Run(step, order);
    }

    [Fact]
    public async Task Another_drivers_order_is_refused_before_the_incident_check_C10()
    {
        var s = new Setup();
        var order = s.OrderBefore(Step.CollectCash);
        s.IncidentBy(s.Driver, order);
        order.ReassignDriver(s.PreviousProfile.Id, 99, "Đổi tài xế");
        order.ConfirmArrival(s.PreviousDriver.Id, Now);

        var error = await Assert.ThrowsAsync<DomainException>(() => s.Run(Step.CollectCash, order));

        Assert.Equal("ORDER_NOT_YOURS", error.Code);
    }
}
