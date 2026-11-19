using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ModMove.Application;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Orders.Commands.AutoCompleteDeliveredOrders;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Infrastructure;
using ModMove.Infrastructure.BackgroundJobs;

namespace ModMove.IntegrationTests;

/// <summary>
/// JOB-ORDER-01 (GB-23, GB-14, GB-15, DECISIONS 7.3, M1, B12, 8.3):
/// auto-completes Delivered orders after 24 hours when there is no open complaint, settles the driver's wallet,
/// and notifies the customer and driver.
/// </summary>
public class AutoCompleteDeliveredOrdersJobTests
{
    private static readonly DateTimeOffset Now = TestApp.Start;

    private static (TestApp App, User Customer, User Driver, DriverProfile Profile, DriverWallet Wallet) CreateSetup()
    {
        var customer = User.Create("Khách", "0900000001", "khach@x.vn", "h", UserRole.Customer, emailVerified: true);
        var driver = User.Create("Tài", "0900000003", "tai@x.vn", "h", UserRole.Driver, emailVerified: true);
        var app = new TestApp(signedInUserId: null, customer, driver);

        var profile = DriverProfile.Create(driver.Id, "front.jpg", "back.jpg");
        app.DriverProfiles.Add(profile);

        var wallet = DriverWallet.Create(profile.Id);
        wallet.TopUpDeposit(300_000m);
        app.DriverWallets.Add(wallet);

        app.Settings.Add(PlatformSetting.Create(SettingKeys.CommissionRate, "0.10", Now));
        return (app, customer, driver, profile, wallet);
    }

    private static ShippingOrder AddDeliveredOrder(
        TestApp app,
        long customerId,
        long driverProfileId,
        long driverUserId,
        string code,
        PaymentMethod method,
        DateTimeOffset deliveredAt,
        decimal distanceFee = 200_000m,
        decimal discount = 20_000m)
    {
        var createdAt = deliveredAt.AddHours(-2);
        var order = ShippingOrder.Create(
            code,
            customerId,
            vehicleTypeId: 1L,
            couponId: null,
            "1 Đinh Tiên Hoàng",
            21.0300m,
            105.8550m,
            "100 Cầu Giấy",
            21.0362m,
            105.7906m,
            "Người nhận",
            "0912345678",
            12m,
            30,
            hasLoadingService: false,
            distanceFee: distanceFee,
            serviceFee: 0m,
            discountAmount: discount,
            method,
            method == PaymentMethod.Cash ? PayerRole.Sender : null,
            CargoDetail.Create("FURNITURE", 1, 50, 100, 50, 50, ["a.jpg"], null),
            createdAt);
        order.CreatedAt = createdAt;
        if (method == PaymentMethod.PayOS)
            order.MarkPaid(createdAt.AddMinutes(1));
        order.Accept(driverProfileId, driverUserId, createdAt.AddMinutes(5));
        order.ConfirmArrival(driverUserId, createdAt.AddMinutes(15));
        order.CollectSenderCash(createdAt.AddMinutes(20));
        order.ConfirmPickup(["1.jpg", "2.jpg"], driverUserId, createdAt.AddMinutes(20));
        order.ApprovePickupEvidence(customerId, createdAt.AddMinutes(25));
        order.Deliver("pod.jpg", driverUserId, deliveredAt);
        app.Orders.Add(order);
        return order;
    }

    [Fact]
    public async Task Auto_completes_PayOS_order_after_24_hours_credits_driver_earnings_and_notifies_JOB_ORDER_01_GB23_M1_8_3()
    {
        var (app, customer, driver, profile, wallet) = CreateSetup();
        var order = AddDeliveredOrder(
            app, customer.Id, profile.Id, driver.Id, "MM20261019001", PaymentMethod.PayOS, deliveredAt: Now.AddHours(-24));

        var count = await app.Sender.Send(new AutoCompleteDeliveredOrdersCommand());

        Assert.Equal(1, count);
        Assert.Equal(OrderStatus.Completed, order.OrderStatus);
        Assert.Equal(Now, order.CompletedAt);
        Assert.Equal(ChangedByRole.System, order.StatusHistories.Last().ChangedByRole);
        Assert.Equal((0.10m, 18_000m, 162_000m), (order.CommissionRate, order.CommissionAmount, order.DriverEarning));

        Assert.Equal(162_000m, wallet.EarningsBalance);
        Assert.Equal(300_000m, wallet.DepositBalance);
        var earningTx = wallet.Transactions.Last();
        Assert.Equal((WalletTransactionType.TripEarning, order.Id, 162_000m), (earningTx.TransactionType, earningTx.OrderId, earningTx.Amount));
        Assert.Equal(1, app.UnitOfWork.Saves);

        Assert.Contains(app.Realtime.Sent, x =>
            x.EventName == RealtimeEvents.OrderStatusChanged && x.Target == "order" && x.Id == order.Id.ToString());
        Assert.Contains(app.Realtime.Sent, x =>
            x.EventName == RealtimeEvents.OrderStatusChanged && x.Target == "user" && x.Id == driver.Id.ToString());

        var notifs = app.Realtime.Sent.Where(x => x.EventName == RealtimeEvents.NotificationReceived).ToList();
        Assert.Equal(2, notifs.Count); // Both customer and driver notified

        var customerNotif = notifs.Single(x => x.Target == "user" && x.Id == customer.Id.ToString());
        var cNotif = Assert.IsType<Notification>(customerNotif.Payload);
        Assert.Equal("ORDER_COMPLETED", cNotif.Type);
        Assert.Contains(order.OrderCode, cNotif.Body);

        var driverNotif = notifs.Single(x => x.Target == "user" && x.Id == driver.Id.ToString());
        var dNotif = Assert.IsType<Notification>(driverNotif.Payload);
        Assert.Equal("ORDER_COMPLETED", dNotif.Type);
        Assert.Contains(order.OrderCode, dNotif.Body);
    }

    [Fact]
    public async Task Auto_completes_cash_order_after_24_hours_and_deducts_commission_from_deposit_GB15()
    {
        var (app, customer, driver, profile, wallet) = CreateSetup();
        typeof(DriverWallet).GetProperty("DepositBalance")!.SetValue(wallet, 5_000m);
        typeof(DriverWallet).GetProperty("WalletStatus")!.SetValue(wallet, WalletStatus.Frozen);

        var order = AddDeliveredOrder(
            app, customer.Id, profile.Id, driver.Id, "MM20261019002", PaymentMethod.Cash, deliveredAt: Now.AddHours(-24));

        var count = await app.Sender.Send(new AutoCompleteDeliveredOrdersCommand());

        Assert.Equal(1, count);
        Assert.Equal(OrderStatus.Completed, order.OrderStatus);
        Assert.Equal(-13_000m, wallet.DepositBalance); // 5,000 − 18,000 commission (GB-15: wallet never blocks completion)
        var deductionTx = wallet.Transactions.Last();
        Assert.Equal((WalletTransactionType.CommissionDeduction, order.Id, 18_000m), (deductionTx.TransactionType, deductionTx.OrderId, deductionTx.Amount));
    }

    [Fact]
    public async Task Skips_orders_under_24_hours_and_orders_with_open_complaints_until_complaint_closes_7_3()
    {
        var (app, customer, driver, profile, wallet) = CreateSetup();

        // 1. Delivered 23h59m59s ago -> not yet 24h
        var recentOrder = AddDeliveredOrder(
            app, customer.Id, profile.Id, driver.Id, "MM20261019003", PaymentMethod.PayOS,
            deliveredAt: Now.AddHours(-24).AddSeconds(1));

        // 2. Delivered 25h ago with a Requested complaint -> held by open complaint (DECISIONS 7.3)
        var complainedOrder = AddDeliveredOrder(
            app, customer.Id, profile.Id, driver.Id, "MM20261019004", PaymentMethod.PayOS,
            deliveredAt: Now.AddHours(-25));
        var complaint = complainedOrder.FileComplaint(customer.Id, "Hàng hư hỏng", null, 50_000m, Now.AddHours(-10));
        app.Refunds.Add(complaint);

        var firstRun = await app.Sender.Send(new AutoCompleteDeliveredOrdersCommand());

        Assert.Equal(0, firstRun);
        Assert.Equal(OrderStatus.Delivered, recentOrder.OrderStatus);
        Assert.Equal(OrderStatus.Delivered, complainedOrder.OrderStatus);
        Assert.Equal(0m, wallet.EarningsBalance);
        Assert.Equal(0, app.UnitOfWork.Saves);
        Assert.Empty(app.Realtime.Sent);

        // Once the complaint is closed (e.g. Rejected), the next run auto-completes the order
        complaint.RejectRefund(adminId: 99L, Now);

        var secondRun = await app.Sender.Send(new AutoCompleteDeliveredOrdersCommand());

        Assert.Equal(1, secondRun);
        Assert.Equal(OrderStatus.Completed, complainedOrder.OrderStatus);
        Assert.Equal(162_000m, wallet.EarningsBalance);
        Assert.Equal(1, app.UnitOfWork.Saves);
    }

    private sealed class SingleTickSender : ISender
    {
        private readonly TaskCompletionSource<object> _sent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<object> FirstCommand => _sent.Task;

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            _sent.TrySetResult(request);
            return Task.FromResult<object?>(0);
        }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    [Fact]
    public async Task AutoCompleteDeliveredOrdersJob_is_registered_and_dispatches_AutoCompleteDeliveredOrdersCommand_JOB_ORDER_01()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=none;User=x;Password=y",
            ["Jwt:SigningKey"] = new string('k', 48),
        }).Build();

        var services = new ServiceCollection().AddLogging().AddApplication().AddInfrastructure(config);
        Assert.Contains(services, d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(AutoCompleteDeliveredOrdersJob));

        var sender = new SingleTickSender();
        var scopes = new ServiceCollection().AddSingleton<ISender>(sender).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var job = new AutoCompleteDeliveredOrdersJob(scopes, TimeProvider.System, NullLogger<AutoCompleteDeliveredOrdersJob>.Instance);

        await HostedJobRunner.RunUntilAsync(job, sender.FirstCommand);
        var command = await sender.FirstCommand;

        Assert.IsType<AutoCompleteDeliveredOrdersCommand>(command);
    }
}

