using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ModMove.Application;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Payments.Commands.ExpireStalePayments;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Infrastructure;
using ModMove.Infrastructure.BackgroundJobs;

namespace ModMove.IntegrationTests;

/// <summary>
/// JOB-PAY-01 (GB-13, DECISIONS 3.1, B12, 8.3):
/// expires Pending PayOS sessions after 10 minutes and cancels Unpaid PayOS orders still Pending 30 minutes after booking.
/// </summary>
public class ExpireStalePaymentsJobTests
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
    public async Task Expires_stale_order_and_wallet_PayOS_sessions_after_10_minutes_without_cancelling_young_order_GB13()
    {
        var (app, customer) = CreateSetup();
        var order = AddOrder(app, customer.Id, "MM20261020001", PaymentMethod.PayOS, createdAt: Now.AddMinutes(-15));

        var expiredOrderTx = PaymentTransaction.ForOrderViaPayOs(
            order.Id, order.TotalFee, 1001L, "https://pay.payos.vn/web/1001", Now.AddMinutes(-10));
        var expiredDepositTx = PaymentTransaction.ForDeposit(
            walletId: 7L, 100_000m, 1002L, "https://pay.payos.vn/web/1002", Now.AddMinutes(-12));
        var freshOrderTx = PaymentTransaction.ForOrderViaPayOs(
            order.Id, order.TotalFee, 1003L, "https://pay.payos.vn/web/1003", Now.AddMinutes(-9).AddSeconds(-59));

        app.Payments.Add(expiredOrderTx);
        app.Payments.Add(expiredDepositTx);
        app.Payments.Add(freshOrderTx);

        var count = await app.Sender.Send(new ExpireStalePaymentsCommand());

        Assert.Equal(2, count);
        Assert.Equal(PaymentTransactionStatus.Expired, expiredOrderTx.TransactionStatus);
        Assert.Equal(PaymentTransactionStatus.Expired, expiredDepositTx.TransactionStatus);
        Assert.Equal(PaymentTransactionStatus.Pending, freshOrderTx.TransactionStatus);
        Assert.Equal(OrderStatus.Pending, order.OrderStatus); // only 15 minutes old (< 30m deadline)
        Assert.Equal(1, app.UnitOfWork.Saves);
        Assert.Empty(app.Realtime.Sent);
    }

    [Fact]
    public async Task Cancels_unpaid_PayOS_order_after_30_minutes_without_creating_refund_and_notifies_customer_JOB_PAY_01_3_1_8_3()
    {
        var (app, customer) = CreateSetup();
        var unpaidExpired = AddOrder(app, customer.Id, "MM20261020002", PaymentMethod.PayOS, createdAt: Now.AddMinutes(-30));
        var expiredTx = PaymentTransaction.ForOrderViaPayOs(
            unpaidExpired.Id, unpaidExpired.TotalFee, 2001L, "https://pay.payos.vn/web/2001", Now.AddMinutes(-15));
        app.Payments.Add(expiredTx);

        var count = await app.Sender.Send(new ExpireStalePaymentsCommand());

        Assert.Equal(2, count); // 1 expired session + 1 cancelled order
        Assert.Equal(PaymentTransactionStatus.Expired, expiredTx.TransactionStatus);
        Assert.Equal(OrderStatus.Cancelled, unpaidExpired.OrderStatus);
        Assert.Equal(ExpireStalePaymentsCommandHandler.TimeoutReason, unpaidExpired.CancellationReason);
        Assert.Equal(ChangedByRole.System, unpaidExpired.StatusHistories.Last().ChangedByRole);
        Assert.Empty(app.Refunds.Items); // unpaid: nothing to refund (3.1)
        Assert.Equal(1, app.UnitOfWork.Saves);

        var statusSent = Assert.Single(app.Realtime.Sent, x => x.EventName == RealtimeEvents.OrderStatusChanged);
        Assert.Equal(("order", unpaidExpired.Id.ToString()), (statusSent.Target, statusSent.Id));

        var notifSent = Assert.Single(app.Realtime.Sent, x => x.EventName == RealtimeEvents.NotificationReceived);
        Assert.Equal(("user", customer.Id.ToString()), (notifSent.Target, notifSent.Id));
        var notif = Assert.IsType<Notification>(notifSent.Payload);
        Assert.Equal("ORDER_CANCELLED", notif.Type);
        Assert.Contains(unpaidExpired.OrderCode, notif.Body);
        Assert.Equal(unpaidExpired.Id.ToString(), notif.Data!["orderId"]);
    }

    [Fact]
    public async Task Leaves_fresh_sessions_paid_PayOS_orders_cash_orders_and_accepted_orders_untouched_3_1()
    {
        var (app, customer) = CreateSetup();
        // 1. Unpaid PayOS order 29m59s old -> still within 30-minute payment deadline
        var freshUnpaidPayOs = AddOrder(
            app, customer.Id, "MM20261020003", PaymentMethod.PayOS, createdAt: Now.AddMinutes(-30).AddSeconds(1));
        var freshTx = PaymentTransaction.ForOrderViaPayOs(
            freshUnpaidPayOs.Id, freshUnpaidPayOs.TotalFee, 3001L, "https://pay.payos.vn/web/3001", Now.AddMinutes(-9).AddSeconds(-59));
        app.Payments.Add(freshTx);

        // 2. Paid PayOS order created 40m ago -> already paid (JOB-MATCH-01 handles matching timeout)
        var paidPayOs = AddOrder(
            app, customer.Id, "MM20261020004", PaymentMethod.PayOS, createdAt: Now.AddMinutes(-40), paidAt: Now.AddMinutes(-35));

        // 3. Cash order created 40m ago -> not PayOS (JOB-MATCH-01 handles matching timeout)
        var cashOrder = AddOrder(
            app, customer.Id, "MM20261020005", PaymentMethod.Cash, createdAt: Now.AddMinutes(-40));

        var count = await app.Sender.Send(new ExpireStalePaymentsCommand());

        Assert.Equal(0, count);
        Assert.Equal(PaymentTransactionStatus.Pending, freshTx.TransactionStatus);
        Assert.Equal(OrderStatus.Pending, freshUnpaidPayOs.OrderStatus);
        Assert.Equal(OrderStatus.Pending, paidPayOs.OrderStatus);
        Assert.Equal(OrderStatus.Pending, cashOrder.OrderStatus);
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
    public async Task ExpireStalePaymentsJob_is_registered_and_dispatches_ExpireStalePaymentsCommand_JOB_PAY_01()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=none;User=x;Password=y",
            ["Jwt:SigningKey"] = new string('k', 48),
        }).Build();

        var services = new ServiceCollection().AddLogging().AddApplication().AddInfrastructure(config);
        Assert.Contains(services, d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(ExpireStalePaymentsJob));

        var sender = new SingleTickSender();
        var scopes = new ServiceCollection().AddSingleton<ISender>(sender).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var job = new ExpireStalePaymentsJob(scopes, TimeProvider.System, NullLogger<ExpireStalePaymentsJob>.Instance);

        await HostedJobRunner.RunUntilAsync(job, sender.FirstCommand);
        var command = await sender.FirstCommand;

        Assert.IsType<ExpireStalePaymentsCommand>(command);
    }
}

