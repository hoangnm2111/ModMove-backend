using System.Data;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ModMove.Application;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Features.Orders.Queries.GetMyOrderDetail;
using ModMove.Application.Features.Orders.Queries.GetMyOrders;
using ModMove.Domain.Enums;

namespace ModMove.IntegrationTests;

/// <summary>GET api/customer/orders and orders/{id} (CUS-08..10, 20) through the real pipeline; rows served by FakeSql.</summary>
public class MyOrdersQueryTests
{
    private const long CustomerId = 7;
    private static readonly DateTime Created = new(2026, 10, 20, 8, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    private sealed class Db
    {
        public FakeSql Sql { get; } = new();
        public string AccountStatus { get; set; } = "Active";
        public DataTable Orders { get; set; } = OrderRows(1);
        public DataTable Cargo { get; set; } = CargoRows();
        public DataTable History { get; set; } = HistoryRows();
        public DataTable Proof { get; set; } = NoRows(ProofRows());
        public DataTable Payment { get; set; } = NoRows(PaymentRows("Pending", Now.AddMinutes(5)));
        public MutableTime Time { get; } = new(Now);
        public List<(string Sql, IReadOnlyDictionary<string, object?> Parameters)> Executed { get; } = [];

        public Db()
        {
            Sql.ScalarResult = Orders.Rows.Count;
            Sql.CustomQuery = (text, parameters) =>
            {
                Executed.Add((text, parameters));
                if (text.Contains("FROM users"))
                    return Table(("AccountStatus", typeof(string), AccountStatus));
                if (text.Contains("FROM cargo_details"))
                    return Cargo;
                if (text.Contains("FROM order_status_histories"))
                    return History;
                if (text.Contains("FROM proof_of_deliveries"))
                    return Proof;
                if (text.Contains("FROM payment_transactions"))
                    return Payment;
                return Orders;
            };
        }

        public ISender Sender() => new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddSingleton<ISqlConnectionFactory>(Sql)
            .AddSingleton<IFileStorage>(new FakeFileStorage())
            .AddSingleton<ICurrentUserService>(new FakeCurrentUser(CustomerId))
            .AddSingleton<TimeProvider>(Time)
            .BuildServiceProvider()
            .GetRequiredService<ISender>();

        public (string Sql, IReadOnlyDictionary<string, object?> Parameters) OrdersQuery() =>
            Executed.Last(e => e.Sql.Contains("FROM shipping_orders"));
    }

    private static DataTable Table(params (string Name, Type Type, object? Value)[] columns)
    {
        var table = new DataTable();
        foreach (var c in columns)
            table.Columns.Add(c.Name, c.Type);
        table.Rows.Add(columns.Select(c => c.Value ?? DBNull.Value).ToArray());
        return table;
    }

    private static DataTable OrderRows(int count)
    {
        var table = Table(
            ("Id", typeof(long), 1L), ("OrderCode", typeof(string), "MM20261020001"), ("OrderStatus", typeof(string), "Pending"),
            ("PickupAddress", typeof(string), "Hoàn Kiếm"), ("DropoffAddress", typeof(string), "Cầu Giấy"),
            ("VehicleTypeName", typeof(string), "Xe van 500kg"), ("TotalFee", typeof(decimal), 250_000m),
            ("PaymentMethod", typeof(string), "Cash"), ("PaymentStatus", typeof(string), "Unpaid"), ("CreatedAt", typeof(DateTime), Created),
            ("ReceiverName", typeof(string), "Bình"), ("ReceiverPhone", typeof(string), "0912345678"),
            ("EstimatedDistanceKm", typeof(decimal), 12m), ("EstimatedDurationMin", typeof(int), 30),
            ("HasLoadingService", typeof(bool), true), ("DistanceFee", typeof(decimal), 170_000m), ("ServiceFee", typeof(decimal), 100_000m),
            ("DiscountAmount", typeof(decimal), 20_000m), ("PayerRole", typeof(string), "Sender"),
            ("DriverName", typeof(string), null), ("DriverPhone", typeof(string), null), ("LicensePlate", typeof(string), null),
            ("CouponCode", typeof(string), "GIAM20K"));
        if (count == 0)
            table.Rows.Clear();
        return table;
    }

    private static DataTable CargoRows() => Table(
        ("CargoType", typeof(string), "FURNITURE"), ("Quantity", typeof(int), 2), ("WeightKg", typeof(decimal), 100m),
        ("LengthCm", typeof(int), 100), ("WidthCm", typeof(int), 100), ("HeightCm", typeof(int), 100),
        ("PhotoUrls", typeof(string), """["https://res.cloudinary.com/demo/a.jpg"]"""), ("SpecialNotes", typeof(string), "Dễ vỡ"),
        ("CargoTypeName", typeof(string), "Đồ nội thất"));

    private static DataTable ProofRows() => Table(
        ("PickupPhotoUrls", typeof(string), """["https://res.cloudinary.com/demo/p1.jpg","https://res.cloudinary.com/demo/p2.jpg"]"""),
        ("ProofStatus", typeof(string), "Rejected"), ("PickupSubmissionCount", typeof(int), 2), ("RejectionReason", typeof(string), "Ảnh mờ"),
        ("DeliveryPhotoUrl", typeof(string), null), ("DeliveryCompletionTime", typeof(DateTime), null));

    private static DataTable NoRows(DataTable table)
    {
        table.Rows.Clear();
        return table;
    }

    private static DataTable PaymentRows(string status, DateTimeOffset expiresAt, DateTime? paidAt = null) => Table(
        ("TransactionStatus", typeof(string), status), ("CheckoutUrl", typeof(string), "https://pay.payos.vn/web/abc"),
        ("ExpiresAt", typeof(DateTime), expiresAt.UtcDateTime), ("PaidAt", typeof(DateTime), paidAt));

    private static DataTable HistoryRows() => Table(
        ("FromStatus", typeof(string), null), ("ToStatus", typeof(string), "Pending"), ("ChangedByRole", typeof(string), "Customer"),
        ("Note", typeof(string), null), ("ChangedAt", typeof(DateTime), Created));

    [Fact]
    public async Task GetMyOrderDetail_signs_private_photos_DEF011()
    {
        var db = new Db { Proof = ProofRows() };
        db.Cargo.Rows[0]["PhotoUrls"] = FakeFileStorage.MixedPhotosJson;
        db.Proof.Rows[0]["PickupPhotoUrls"] = FakeFileStorage.MixedPhotosJson;
        db.Proof.Rows[0]["DeliveryPhotoUrl"] = FakeFileStorage.PrivatePhoto;

        var order = await db.Sender().Send(new GetMyOrderDetailQuery(1));

        Assert.Equal(FakeFileStorage.MixedPhotosViewed, order.Cargo.PhotoUrls);
        Assert.Equal(FakeFileStorage.MixedPhotosViewed, order.Proof!.PickupPhotoUrls);
        Assert.Equal(FakeFileStorage.SignedPhoto, order.Proof.DeliveryPhotoUrl);
    }

    [Fact]
    public async Task List_reads_only_the_callers_orders()
    {
        var db = new Db();

        var page = await db.Sender().Send(new GetMyOrdersQuery());

        var (sql, parameters) = db.OrdersQuery();
        Assert.Contains("o.customer_id = @CustomerId", sql);
        Assert.Equal(CustomerId, parameters["CustomerId"]);
        Assert.Equal("MM20261020001", page.Items.Single().OrderCode);
        Assert.Equal(new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero), page.Items.Single().CreatedAt);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task History_view_is_a_parameter()
    {
        var db = new Db();

        await db.Sender().Send(new GetMyOrdersQuery(View: OrderView.History));

        Assert.Equal("History", db.OrdersQuery().Parameters["View"]);
    }

    [Fact]
    public async Task Search_is_a_parameter_not_sql_text()
    {
        var db = new Db();

        await db.Sender().Send(new GetMyOrdersQuery(Search: "x' OR 1=1"));

        var (sql, parameters) = db.OrdersQuery();
        Assert.DoesNotContain("OR 1=1", sql);
        Assert.Equal("%x' OR 1=1%", parameters["Pattern"]);
    }

    [Fact]
    public async Task Search_escapes_like_wildcards()
    {
        var db = new Db();

        await db.Sender().Send(new GetMyOrdersQuery(Search: "50%_off"));

        Assert.Equal(@"%50\%\_off%", db.OrdersQuery().Parameters["Pattern"]);
    }

    [Theory]
    [InlineData(OrderView.Completed, "Completed")]
    [InlineData(OrderView.Cancelled, "Cancelled")]
    public async Task Completed_and_cancelled_are_separate_views_CUS08(OrderView view, string expected)
    {
        var db = new Db();

        await db.Sender().Send(new GetMyOrdersQuery(View: view));

        var (sql, parameters) = db.OrdersQuery();
        Assert.Equal(expected, parameters["View"]);
        Assert.Contains($"@View = '{expected}'", sql);
    }

    [Fact]
    public async Task List_item_shows_the_receiver_CUS08()
    {
        var item = (await new Db().Sender().Send(new GetMyOrdersQuery())).Items.Single();

        Assert.Equal(("Bình", "0912345678"), (item.ReceiverName, item.ReceiverPhone));
    }

    [Fact]
    public async Task Filters_are_parameters_CUS09()
    {
        var db = new Db();

        await db.Sender().Send(new GetMyOrdersQuery(
            Status: OrderStatus.InTransit, FromDate: new DateOnly(2026, 10, 20), ToDate: new DateOnly(2026, 10, 21), ReceiverPhone: "0912"));

        var (sql, parameters) = db.OrdersQuery();
        Assert.Contains("o.order_status = @Status", sql);
        Assert.Contains("o.created_at >= @FromUtc", sql);
        Assert.Contains("o.created_at < @ToUtc", sql);
        Assert.Contains("o.receiver_phone LIKE @PhonePattern", sql);
        Assert.Equal("InTransit", parameters["Status"]);
        Assert.Equal("%0912%", parameters["PhonePattern"]);
        // Vietnam days (UTC+7): from 20/10 00:00 VN up to, not including, 22/10 00:00 VN.
        Assert.Equal(new DateTime(2026, 10, 19, 17, 0, 0, DateTimeKind.Utc), parameters["FromUtc"]);
        Assert.Equal(new DateTime(2026, 10, 21, 17, 0, 0, DateTimeKind.Utc), parameters["ToUtc"]);
    }

    [Fact]
    public async Task No_filters_bind_nulls()
    {
        var db = new Db();

        await db.Sender().Send(new GetMyOrdersQuery());

        var parameters = db.OrdersQuery().Parameters;
        // Dapper sends a null parameter as DBNull.
        Assert.All(["Status", "PhonePattern", "FromUtc", "ToUtc"], name => Assert.Equal(DBNull.Value, parameters[name]));
    }

    [Fact]
    public async Task From_date_after_to_date_is_rejected() =>
        await Assert.ThrowsAsync<ValidationException>(() => new Db().Sender().Send(
            new GetMyOrdersQuery(FromDate: new DateOnly(2026, 10, 21), ToDate: new DateOnly(2026, 10, 20))));

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(9999, 12, 31)]
    public async Task Out_of_range_date_is_rejected_not_a_server_error(int year, int month, int day)
    {
        var date = new DateOnly(year, month, day);

        await Assert.ThrowsAsync<ValidationException>(() => new Db().Sender().Send(new GetMyOrdersQuery(FromDate: date)));
        await Assert.ThrowsAsync<ValidationException>(() => new Db().Sender().Send(new GetMyOrdersQuery(ToDate: date)));
    }

    [Theory]
    [InlineData("09a1")]
    [InlineData("0912345678901234")]
    public async Task Receiver_phone_filter_is_digits_up_to_15(string phone) =>
        await Assert.ThrowsAsync<ValidationException>(() => new Db().Sender().Send(new GetMyOrdersQuery(ReceiverPhone: phone)));

    [Fact]
    public async Task Unknown_status_filter_is_rejected() =>
        await Assert.ThrowsAsync<ValidationException>(() => new Db().Sender().Send(new GetMyOrdersQuery(Status: (OrderStatus)99)));

    [Fact]
    public async Task Huge_page_number_is_rejected_not_a_server_error() =>
        await Assert.ThrowsAsync<ValidationException>(() => new Db().Sender().Send(new GetMyOrdersQuery(Page: int.MaxValue)));

    [Fact]
    public async Task Page_size_over_50_is_rejected() =>
        await Assert.ThrowsAsync<ValidationException>(() => new Db().Sender().Send(new GetMyOrdersQuery(PageSize: 51)));

    [Fact]
    public async Task Locked_account_cannot_list_orders_B8()
    {
        var db = new Db { AccountStatus = "Locked" };

        var error = await Assert.ThrowsAsync<ForbiddenAccessException>(() => db.Sender().Send(new GetMyOrdersQuery()));

        Assert.Equal("ACCOUNT_INACTIVE", error.Code);
    }

    [Fact]
    public async Task Detail_has_cargo_photos_and_history()
    {
        var db = new Db();

        var order = await db.Sender().Send(new GetMyOrderDetailQuery(1));

        var (sql, parameters) = db.OrdersQuery();
        Assert.Contains("o.customer_id = @CustomerId", sql);
        Assert.Equal(1L, parameters["OrderId"]);
        Assert.Equal(["https://res.cloudinary.com/demo/a.jpg"], order.Cargo.PhotoUrls);
        Assert.Equal("Pending", order.History.Single().ToStatus);
        Assert.Null(order.Driver);
    }

    [Fact]
    public async Task Detail_points_at_the_incident_that_holds_the_trip_7_6()
    {
        var db = new Db();
        db.Orders.Columns.Add("PendingDriverIncidentId", typeof(long));
        db.Orders.Rows[0]["PendingDriverIncidentId"] = 42L;

        var order = await db.Sender().Send(new GetMyOrderDetailQuery(1));

        Assert.Equal(42L, order.PendingDriverIncidentId);
        // Only an Open or InProgress incident reported by the driver who now holds the order counts (not the customer's own).
        var (sql, _) = db.OrdersQuery();
        Assert.Contains("i.reported_by = dp.user_id", sql);
        Assert.Contains("i.resolution_status IN ('Open', 'InProgress')", sql);
    }

    [Fact]
    public async Task Detail_has_the_sender_and_the_notes_of_both_stops()
    {
        var db = new Db();
        foreach (var column in new[] { "SenderName", "SenderPhone", "PickupNote", "DropoffNote" })
            db.Orders.Columns.Add(column, typeof(string));
        db.Orders.Rows[0]["SenderName"] = "Chị Hoa";
        db.Orders.Rows[0]["SenderPhone"] = "0987654321";
        db.Orders.Rows[0]["PickupNote"] = "Cửa hầm B1";
        db.Orders.Rows[0]["DropoffNote"] = "Gọi trước 15 phút";

        var order = await db.Sender().Send(new GetMyOrderDetailQuery(1));

        Assert.Equal(("Chị Hoa", "0987654321"), (order.SenderName, order.SenderPhone));
        Assert.Equal(("Cửa hầm B1", "Gọi trước 15 phút"), (order.PickupNote, order.DropoffNote));
        var (sql, _) = db.OrdersQuery();
        Assert.Contains("o.sender_name AS SenderName", sql);
        Assert.Contains("o.pickup_note AS PickupNote", sql);
    }

    [Fact]
    public async Task Detail_of_an_order_booked_before_the_sender_existed_has_no_sender_or_notes()
    {
        var order = await new Db().Sender().Send(new GetMyOrderDetailQuery(1));

        Assert.Null(order.SenderName);
        Assert.Null(order.PickupNote);
    }

    [Fact]
    public async Task Detail_has_no_holding_incident_by_default() =>
        Assert.Null((await new Db().Sender().Send(new GetMyOrderDetailQuery(1))).PendingDriverIncidentId);

    [Fact]
    public async Task Detail_has_no_photos_section_before_pickup()
    {
        var order = await new Db().Sender().Send(new GetMyOrderDetailQuery(1));

        Assert.Null(order.Proof);
    }

    [Fact]
    public async Task Detail_shows_the_pickup_photos_to_review_CUS11()
    {
        var db = new Db { Proof = ProofRows() };

        var proof = (await db.Sender().Send(new GetMyOrderDetailQuery(1))).Proof!;

        Assert.Equal(2, proof.PickupPhotoUrls.Count);
        Assert.Equal(("Rejected", 2, "Ảnh mờ"), (proof.ProofStatus, proof.PickupSubmissionCount, proof.RejectionReason));
        Assert.Null(proof.DeliveryPhotoUrl);
        Assert.Equal(1L, db.Executed.Last(e => e.Sql.Contains("FROM proof_of_deliveries")).Parameters["OrderId"]);
    }

    [Fact]
    public async Task Proof_without_pickup_photos_reads_as_an_empty_list()
    {
        var proof = ProofRows();
        proof.Rows[0]["PickupPhotoUrls"] = DBNull.Value;
        var db = new Db { Proof = proof };

        var order = await db.Sender().Send(new GetMyOrderDetailQuery(1));

        Assert.Empty(order.Proof!.PickupPhotoUrls);
    }

    [Fact]
    public async Task Detail_shows_coupon_code_and_cargo_type_name_CUS10()
    {
        var order = await new Db().Sender().Send(new GetMyOrderDetailQuery(1));

        Assert.Equal("GIAM20K", order.CouponCode);
        Assert.Equal(("FURNITURE", "Đồ nội thất"), (order.Cargo.CargoType, order.Cargo.CargoTypeName));
    }

    [Fact]
    public async Task Detail_has_no_payment_before_a_payos_link_exists()
    {
        var order = await new Db().Sender().Send(new GetMyOrderDetailQuery(1));

        Assert.Null(order.Payment);
    }

    [Fact]
    public async Task Detail_shows_the_open_payos_link_CUS27()
    {
        var db = new Db { Payment = PaymentRows("Pending", Now.AddMinutes(5)) };

        var payment = (await db.Sender().Send(new GetMyOrderDetailQuery(1))).Payment!;

        Assert.Equal(("Pending", "https://pay.payos.vn/web/abc", false), (payment.TransactionStatus, payment.CheckoutUrl, payment.IsExpired));
        Assert.Equal(Now.AddMinutes(5), payment.ExpiresAt);
        var (sql, parameters) = db.Executed.Last(e => e.Sql.Contains("FROM payment_transactions"));
        Assert.Contains("payment_method = 'PayOS'", sql);
        Assert.Equal(1L, parameters["OrderId"]);
    }

    [Fact]
    public async Task Expired_payos_link_is_hidden_and_marked_expired_3_1()
    {
        var db = new Db { Payment = PaymentRows("Pending", Now.AddMinutes(-1)) };

        var payment = (await db.Sender().Send(new GetMyOrderDetailQuery(1))).Payment!;

        Assert.True(payment.IsExpired);
        Assert.Null(payment.CheckoutUrl);
    }

    [Fact]
    public async Task Transaction_marked_expired_is_expired_3_1()
    {
        var db = new Db { Payment = PaymentRows("Expired", Now.AddMinutes(-1)) };

        var payment = (await db.Sender().Send(new GetMyOrderDetailQuery(1))).Payment!;

        Assert.Equal(("Expired", true), (payment.TransactionStatus, payment.IsExpired));
        Assert.Null(payment.CheckoutUrl);
    }

    [Fact]
    public async Task Cancelled_order_has_no_payment_so_it_cannot_be_paid_any_more()
    {
        var db = new Db { Payment = PaymentRows("Pending", Now.AddMinutes(5)) };
        db.Orders.Rows[0]["OrderStatus"] = "Cancelled";

        var order = await db.Sender().Send(new GetMyOrderDetailQuery(1));

        Assert.Null(order.Payment);
    }

    [Fact]
    public async Task Paid_transaction_shows_when_it_was_paid()
    {
        var paidAt = new DateTime(2026, 10, 20, 8, 30, 0, DateTimeKind.Unspecified);
        var db = new Db { Payment = PaymentRows("Paid", Now.AddMinutes(-20), paidAt) };

        var payment = (await db.Sender().Send(new GetMyOrderDetailQuery(1))).Payment!;

        Assert.Equal(("Paid", false), (payment.TransactionStatus, payment.IsExpired));
        Assert.Null(payment.CheckoutUrl);
        Assert.Equal(new DateTimeOffset(paidAt, TimeSpan.Zero), payment.PaidAt);
    }

    [Fact]
    public async Task Locked_account_cannot_read_an_order_B8()
    {
        var db = new Db { AccountStatus = "Locked" };

        var error = await Assert.ThrowsAsync<ForbiddenAccessException>(() => db.Sender().Send(new GetMyOrderDetailQuery(1)));

        Assert.Equal("ACCOUNT_INACTIVE", error.Code);
    }

    [Fact]
    public async Task Other_customers_order_is_404()
    {
        var db = new Db { Orders = OrderRows(0) };

        await Assert.ThrowsAsync<NotFoundException>(() => db.Sender().Send(new GetMyOrderDetailQuery(99)));
    }
}
