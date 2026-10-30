using System.Data;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ModMove.Application;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Features.Orders.Queries.GetMyOrderDetail;
using ModMove.Application.Features.Orders.Queries.GetMyOrders;

namespace ModMove.IntegrationTests;

/// <summary>GET api/customer/orders and orders/{id} (CUS-08..10, 20) through the real pipeline; rows served by FakeSql.</summary>
public class MyOrdersQueryTests
{
    private const long CustomerId = 7;
    private static readonly DateTime Created = new(2026, 10, 20, 8, 0, 0, DateTimeKind.Unspecified);

    private sealed class Db
    {
        public FakeSql Sql { get; } = new();
        public string AccountStatus { get; set; } = "Active";
        public DataTable Orders { get; set; } = OrderRows(1);
        public DataTable Cargo { get; set; } = CargoRows();
        public DataTable History { get; set; } = HistoryRows();
        public DataTable Proof { get; set; } = NoRows(ProofRows());
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
                return Orders;
            };
        }

        public ISender Sender() => new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddSingleton<ISqlConnectionFactory>(Sql)
            .AddSingleton<ICurrentUserService>(new FakeCurrentUser(CustomerId))
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
            ("DriverName", typeof(string), null), ("DriverPhone", typeof(string), null), ("LicensePlate", typeof(string), null));
        if (count == 0)
            table.Rows.Clear();
        return table;
    }

    private static DataTable CargoRows() => Table(
        ("CargoType", typeof(string), "FURNITURE"), ("Quantity", typeof(int), 2), ("WeightKg", typeof(decimal), 100m),
        ("LengthCm", typeof(int), 100), ("WidthCm", typeof(int), 100), ("HeightCm", typeof(int), 100),
        ("PhotoUrls", typeof(string), """["https://res.cloudinary.com/demo/a.jpg"]"""), ("SpecialNotes", typeof(string), "Dễ vỡ"));

    private static DataTable ProofRows() => Table(
        ("PickupPhotoUrls", typeof(string), """["https://res.cloudinary.com/demo/p1.jpg","https://res.cloudinary.com/demo/p2.jpg"]"""),
        ("ProofStatus", typeof(string), "Rejected"), ("PickupSubmissionCount", typeof(int), 2), ("RejectionReason", typeof(string), "Ảnh mờ"),
        ("DeliveryPhotoUrl", typeof(string), null), ("DeliveryCompletionTime", typeof(DateTime), null));

    private static DataTable NoRows(DataTable table)
    {
        table.Rows.Clear();
        return table;
    }

    private static DataTable HistoryRows() => Table(
        ("FromStatus", typeof(string), null), ("ToStatus", typeof(string), "Pending"), ("ChangedByRole", typeof(string), "Customer"),
        ("Note", typeof(string), null), ("ChangedAt", typeof(DateTime), Created));

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
    public async Task Other_customers_order_is_404()
    {
        var db = new Db { Orders = OrderRows(0) };

        await Assert.ThrowsAsync<NotFoundException>(() => db.Sender().Send(new GetMyOrderDetailQuery(99)));
    }
}
