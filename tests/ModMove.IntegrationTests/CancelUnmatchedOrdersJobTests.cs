using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ModMove.Application;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Matching.Commands.CancelUnmatchedOrders;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Infrastructure;
using ModMove.Infrastructure.BackgroundJobs;

namespace ModMove.IntegrationTests;

/// <summary>
/// JOB-MATCH-01 (GB-24, GB-41, DECISIONS 3.2, 3.3, B9, B12, 8.3):
/// cancels Pending orders after 10 minutes without a driver and creates a 100% Cancellation refund for Paid PayOS orders.
/// </summary>
public class CancelUnmatchedOrdersJobTests
{
    private static readonly DateTimeOffset Now = TestApp.Start;

    private static (TestApp App, User Customer) CreateSetup()
    {
        var customer = User.Create("Khách", "0900000001", "khach@x.vn", "h", UserRole.Customer, emailVerified: true);
        var app = new TestApp(signedInUserId: null, customer);
        return (app, customer);
    }

    private static ShippingOrder AddOrder(
        TestApp app,
        long customerId,
        string code,
        PaymentMethod method,
        DateTimeOffset createdAt,
        DateTimeOffset? paidAt = null,
        decimal distanceFee = 250_000m)
    {
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
            8.5m,
            25,
            hasLoadingService: false,
            distanceFee: distanceFee,
            serviceFee: 0m,
            discountAmount: 0m,
            method,
            method == PaymentMethod.Cash ? PayerRole.Sender : null,
            CargoDetail.Create("FURNITURE", 1, 50, 100, 50, 50, ["a.jpg"], null),
            createdAt);
        order.CreatedAt = createdAt;
        if (paidAt is not null)
            order.MarkPaid(paidAt.Value);
        app.Orders.Add(order);
        return order;
    }

    [Fact]
    public async Task Cancels_expired_cash_order_without_creating_refund_and_notifies_customer_JOB_MATCH_01_GB24_8_3()
    {
        var (app, customer) = CreateSetup();
        var cashExpired = AddOrder(app, customer.Id, "MM20261020001", PaymentMethod.Cash, createdAt: Now.AddMinutes(-10));

        var count = await app.Sender.Send(new CancelUnmatchedOrdersCommand());

        Assert.Equal(1, count);
        Assert.Equal(OrderStatus.Cancelled, cashExpired.OrderStatus);
        Assert.Equal(CancelUnmatchedOrdersCommandHandler.TimeoutReason, cashExpired.CancellationReason);
        Assert.Equal(ChangedByRole.System, cashExpired.StatusHistories.Last().ChangedByRole);
        Assert.Empty(app.Refunds.Items);
        Assert.Equal(1, app.UnitOfWork.Saves);

        var statusSent = Assert.Single(app.Realtime.Sent, x => x.EventName == RealtimeEvents.OrderStatusChanged);
        Assert.Equal(("order", cashExpired.Id.ToString()), (statusSent.Target, statusSent.Id));

        var notifSent = Assert.Single(app.Realtime.Sent, x => x.EventName == RealtimeEvents.NotificationReceived);
        Assert.Equal(("user", customer.Id.ToString()), (notifSent.Target, notifSent.Id));
        var notif = Assert.IsType<Notification>(notifSent.Payload);
        Assert.Equal("ORDER_CANCELLED", notif.Type);
        Assert.Contains(cashExpired.OrderCode, notif.Body);
        Assert.Equal(cashExpired.Id.ToString(), notif.Data!["orderId"]);
    }

    [Fact]
    public async Task Cancels_expired_paid_payos_order_and_creates_100_percent_cancellation_refund_GB41_3_3()
    {
        var (app, customer) = CreateSetup();
        var paidExpired = AddOrder(
            app,
            customer.Id,
            "MM20261020002",
            PaymentMethod.PayOS,
            createdAt: Now.AddMinutes(-22),
            paidAt: Now.AddMinutes(-10),
            distanceFee: 320_000m);

        var count = await app.Sender.Send(new CancelUnmatchedOrdersCommand());

        Assert.Equal(1, count);
        Assert.Equal(OrderStatus.Cancelled, paidExpired.OrderStatus);

        var refund = Assert.Single(app.Refunds.Items);
        Assert.Equal(paidExpired.Id, refund.OrderId);
        Assert.Equal(RefundSourceType.Cancellation, refund.SourceType);
        Assert.Equal(RefundStatus.Requested, refund.RefundStatus);
        Assert.Equal(320_000m, refund.RequestedAmount);
        Assert.Equal(320_000m, refund.ProposedAmount); // goes straight to Admin without Dispatcher step (B9, 3.3)
        Assert.Null(refund.IncidentId);
    }

    [Fact]
    public async Task Leaves_fresh_orders_unpaid_payos_orders_and_accepted_orders_untouched_3_2()
    {
        var (app, customer) = CreateSetup();
        // 1. Cash order 9m59s old -> still within 10-minute matching window
        var freshCash = AddOrder(app, customer.Id, "MM20261020003", PaymentMethod.Cash, createdAt: Now.AddMinutes(-10).AddSeconds(1));
        // 2. Paid PayOS order created 25m ago, paid 9m59s ago -> still within 10-minute matching window from paid_at (3.2)
        var freshPaidPayOs = AddOrder(
            app,
            customer.Id,
            "MM20261020004",
            PaymentMethod.PayOS,
            createdAt: Now.AddMinutes(-25),
            paidAt: Now.AddMinutes(-10).AddSeconds(1));
        // 3. Unpaid PayOS order created 20m ago -> not in matching pool yet (handled by JOB-PAY-01 at 30m)
        var unpaidPayOs = AddOrder(app, customer.Id, "MM20261020005", PaymentMethod.PayOS, createdAt: Now.AddMinutes(-20), paidAt: null);
        // 4. Already accepted order created 15m ago -> not Pending
        var acceptedOrder = AddOrder(app, customer.Id, "MM20261020006", PaymentMethod.Cash, createdAt: Now.AddMinutes(-15));
        acceptedOrder.Accept(driverProfileId: 5L, driverUserId: 9L, Now.AddMinutes(-14));

        var count = await app.Sender.Send(new CancelUnmatchedOrdersCommand());

        Assert.Equal(0, count);
        Assert.Equal(OrderStatus.Pending, freshCash.OrderStatus);
        Assert.Equal(OrderStatus.Pending, freshPaidPayOs.OrderStatus);
        Assert.Equal(OrderStatus.Pending, unpaidPayOs.OrderStatus);
        Assert.Equal(OrderStatus.Accepted, acceptedOrder.OrderStatus);
        Assert.Equal(0, app.UnitOfWork.Saves);
        Assert.Empty(app.Realtime.Sent);
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
    public async Task CancelUnmatchedOrdersJob_is_registered_and_dispatches_CancelUnmatchedOrdersCommand_JOB_MATCH_01()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=none;User=x;Password=y",
            ["Jwt:SigningKey"] = new string('k', 48),
        }).Build();

        var services = new ServiceCollection().AddLogging().AddApplication().AddInfrastructure(config);
        Assert.Contains(services, d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(CancelUnmatchedOrdersJob));

        var sender = new SingleTickSender();
        var scopes = new ServiceCollection().AddSingleton<ISender>(sender).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var job = new CancelUnmatchedOrdersJob(scopes, TimeProvider.System, NullLogger<CancelUnmatchedOrdersJob>.Instance);

        await job.StartAsync(CancellationToken.None);
        var command = await sender.FirstCommand.WaitAsync(TimeSpan.FromSeconds(5));
        await job.StopAsync(CancellationToken.None);

        Assert.IsType<CancelUnmatchedOrdersCommand>(command);
    }
}

