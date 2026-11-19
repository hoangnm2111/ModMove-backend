using ModMove.Application.Common.Interfaces;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.IntegrationTests;

/// <summary>
/// A signed-in Dispatcher, a customer and a driver with a wallet; orders and incidents walked to the step a test needs.
/// Like <see cref="TripSetup"/>, but the caller is the Dispatcher.
/// </summary>
internal sealed class DispatcherSetup
{
    public static readonly DateTimeOffset Now = TestApp.Start;

    public TestApp App { get; }
    public User Dispatcher { get; }
    public User Customer { get; }
    public User Driver { get; }
    public DriverProfile Profile { get; }
    public DriverWallet Wallet { get; }

    public DispatcherSetup()
    {
        Dispatcher = User.Create("Điều phối", "0900000010", "disp@x.vn", "h", UserRole.Dispatcher, emailVerified: true);
        Customer = User.Create("An", "0900000001", "an@x.vn", "h", UserRole.Customer, emailVerified: true);
        Driver = User.Create("Tài", "0900000003", "tai@x.vn", "h", UserRole.Driver, emailVerified: true);
        App = TestApp.SignedInAs(Dispatcher, Customer, Driver);
        Profile = DriverProfile.Create(Driver.Id, "front.jpg", "back.jpg");
        App.DriverProfiles.Add(Profile);
        Wallet = DriverWallet.Create(Profile.Id);
        Wallet.TopUpDeposit(300_000);
        App.DriverWallets.Add(Wallet);
        App.Settings.Add(PlatformSetting.Create(SettingKeys.CommissionRate, "0.10", Now));
        App.Settings.Add(PlatformSetting.Create(SettingKeys.RequiredDeposit, "200000", Now));
        App.Settings.Add(PlatformSetting.Create(SettingKeys.MatchingRadiusKm, "5", Now));
    }

    /// <summary>Adds an Approved, Online candidate driver with an active vehicle, fresh GPS and a funded wallet.</summary>
    public (User User, DriverProfile Profile, DriverWallet Wallet) ReplacementDriver(
        string suffix = "2",
        long vehicleTypeId = 1,
        decimal latitude = 21.055m,
        decimal longitude = 105.855m,
        DateTimeOffset? locationAt = null,
        decimal deposit = 300_000,
        bool online = true,
        bool approved = true)
    {
        var user = User.Create($"Tài {suffix}", $"09000001{suffix}", $"tai{suffix}@x.vn", "h", UserRole.Driver, emailVerified: true);
        App.Users.Add(user);

        var profile = DriverProfile.Create(user.Id, "f.jpg", "b.jpg");
        App.DriverProfiles.Add(profile);
        if (approved)
        {
            var vehicle = Vehicle.Create(profile.Id, vehicleTypeId, $"29A-100{suffix}", 1000m, "200x150x150", "r.jpg", "i.jpg", "v.jpg");
            profile.Approve(vehicle);
        }
        if (online && approved)
            profile.GoOnline();

        profile.UpdateLocation(latitude, longitude, locationAt ?? Now);

        var wallet = DriverWallet.Create(profile.Id);
        if (deposit > 0)
            wallet.CreditTopUp(deposit);
        App.DriverWallets.Add(wallet);

        return (user, profile, wallet);
    }

    /// <summary>An order of the customer that the driver has accepted; total fee 180,000 for the default discount.</summary>
    public ShippingOrder Accepted(PaymentMethod method = PaymentMethod.PayOS, decimal discount = 20_000)
    {
        var order = ShippingOrder.Create(
            "MM20261020001", Customer.Id, vehicleTypeId: 1, couponId: null,
            "A", 21.0m, 105.8m, "B", 21.1m, 105.9m, "Bình", "0912345678",
            12m, 30, hasLoadingService: false, distanceFee: 200_000, serviceFee: 0, discountAmount: discount,
            method, method == PaymentMethod.Cash ? PayerRole.Sender : null,
            CargoDetail.Create("FURNITURE", 1, 50, 100, 50, 50, ["a.jpg"], null), Now);
        if (method == PaymentMethod.PayOS)
            order.MarkPaid(Now);
        order.Accept(Profile.Id, Driver.Id, Now);
        App.Orders.Add(order);
        return order;
    }

    public ShippingOrder Arrived(PaymentMethod method = PaymentMethod.PayOS)
    {
        var order = Accepted(method);
        order.ConfirmArrival(Driver.Id, Now);
        return order;
    }

    public ShippingOrder PickedUp(PaymentMethod method = PaymentMethod.PayOS)
    {
        var order = Arrived(method);
        order.CollectSenderCash(Now);
        order.ConfirmPickup(["1.jpg", "2.jpg"], Driver.Id, Now);
        return order;
    }

    public ShippingOrder InTransit(PaymentMethod method = PaymentMethod.PayOS)
    {
        var order = PickedUp(method);
        order.ApprovePickupEvidence(order.CustomerId, Now);
        return order;
    }

    public ShippingOrder Delivered(PaymentMethod method = PaymentMethod.PayOS)
    {
        var order = InTransit(method);
        order.Deliver("pod.jpg", Driver.Id, Now);
        return order;
    }

    /// <summary>An incident the driver reported on <paramref name="order"/>, still Open.</summary>
    public InTransitIncident Incident(ShippingOrder order, IncidentType type = IncidentType.VehicleBreakdown)
    {
        var incident = InTransitIncident.Report(order.Id, Driver.Id, type, "Xe hỏng", null, 21.05m, 105.85m);
        App.Incidents.Add(incident);
        return incident;
    }

    /// <summary>A refund the customer asked for because of <paramref name="incident"/> (UC-CUS-17).</summary>
    public Refund IncidentRefund(ShippingOrder order, InTransitIncident incident, decimal requested = 100_000)
    {
        var refund = order.RequestIncidentRefund(Customer.Id, incident.Id, "Hàng hư", null, requested);
        App.Refunds.Add(refund);
        return refund;
    }

    /// <summary>A complaint the customer filed on a delivered <paramref name="order"/> (UC-CUS-21).</summary>
    public Refund Complaint(ShippingOrder order, decimal requested = 100_000)
    {
        var refund = order.FileComplaint(Customer.Id, "Hàng vỡ", null, requested, Now);
        App.Refunds.Add(refund);
        return refund;
    }

    /// <summary>The notifications sent to one user over SignalR (B17), oldest first.</summary>
    public IReadOnlyList<Notification> SentTo(User user) =>
        App.Realtime.Sent
            .Where(x => x is { Target: "user", EventName: RealtimeEvents.NotificationReceived } && x.Id == user.Id.ToString())
            .Select(x => (Notification)x.Payload)
            .ToList();
}
