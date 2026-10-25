using System.Data;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ModMove.Application;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Features.Incidents.Queries.GetIncidentById;

namespace ModMove.IntegrationTests;

/// <summary>GET api/dispatcher/incidents/{id} (UC-DIS-04) through the real pipeline with <see cref="FakeSql"/>.</summary>
public class GetIncidentByIdTests
{
    private static ISender SenderWith(FakeSql sql) => new ServiceCollection()
        .AddLogging()
        .AddApplication()
        .AddSingleton<ISqlConnectionFactory>(sql)
        .BuildServiceProvider()
        .GetRequiredService<ISender>();

    private static DataTable CreateIncidentTable()
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(long));
        table.Columns.Add("OrderId", typeof(long));
        table.Columns.Add("OrderCode", typeof(string));
        table.Columns.Add("OrderStatus", typeof(string));
        table.Columns.Add("IncidentType", typeof(string));
        table.Columns.Add("Description", typeof(string));
        table.Columns.Add("PhotoUrlsJson", typeof(string));
        table.Columns.Add("Latitude", typeof(decimal));
        table.Columns.Add("Longitude", typeof(decimal));
        table.Columns.Add("ResolutionStatus", typeof(string));
        table.Columns.Add("ResolutionDecision", typeof(string));
        table.Columns.Add("ResolutionNote", typeof(string));
        table.Columns.Add("ReportedById", typeof(long));
        table.Columns.Add("ReporterName", typeof(string));
        table.Columns.Add("ReporterRole", typeof(string));
        table.Columns.Add("HandledById", typeof(long));
        table.Columns.Add("HandledByName", typeof(string));
        table.Columns.Add("ResolvedAt", typeof(DateTime));
        table.Columns.Add("CreatedAt", typeof(DateTime));

        // Order fields
        table.Columns.Add("PickupAddress", typeof(string));
        table.Columns.Add("DropoffAddress", typeof(string));
        table.Columns.Add("PaymentMethod", typeof(string));
        table.Columns.Add("PaymentStatus", typeof(string));
        table.Columns.Add("TotalFee", typeof(decimal));
        table.Columns.Add("DeliveredAt", typeof(DateTime));

        // Cargo fields
        table.Columns.Add("CargoType", typeof(string));
        table.Columns.Add("CargoQuantity", typeof(int));
        table.Columns.Add("CargoWeightKg", typeof(decimal));
        table.Columns.Add("CargoLengthCm", typeof(int));
        table.Columns.Add("CargoWidthCm", typeof(int));
        table.Columns.Add("CargoHeightCm", typeof(int));
        table.Columns.Add("CargoSpecialNotes", typeof(string));

        // Customer fields
        table.Columns.Add("CustomerId", typeof(long));
        table.Columns.Add("CustomerFullName", typeof(string));
        table.Columns.Add("CustomerPhone", typeof(string));

        // Driver fields
        table.Columns.Add("DriverProfileId", typeof(long));
        table.Columns.Add("DriverUserId", typeof(long));
        table.Columns.Add("DriverFullName", typeof(string));
        table.Columns.Add("DriverPhone", typeof(string));
        table.Columns.Add("LicensePlate", typeof(string));
        table.Columns.Add("VehicleTypeName", typeof(string));

        return table;
    }

    private static DataTable CreateHistoriesTable()
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(long));
        table.Columns.Add("FromStatus", typeof(string));
        table.Columns.Add("ToStatus", typeof(string));
        table.Columns.Add("ChangedByRole", typeof(string));
        table.Columns.Add("ChangedByUserId", typeof(long));
        table.Columns.Add("ChangedByUserName", typeof(string));
        table.Columns.Add("Note", typeof(string));
        table.Columns.Add("CreatedAt", typeof(DateTime));
        return table;
    }

    private static DataTable CreateRefundTable()
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(long));
        table.Columns.Add("SourceType", typeof(string));
        table.Columns.Add("RequestedAmount", typeof(decimal));
        table.Columns.Add("ProposedAmount", typeof(decimal));
        table.Columns.Add("RefundStatus", typeof(string));
        table.Columns.Add("CreatedAt", typeof(DateTime));
        return table;
    }

    [Fact]
    public async Task When_incident_does_not_exist_throws_NotFoundException()
    {
        var sql = new FakeSql { Result = CreateIncidentTable() };

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => SenderWith(sql).Send(new GetIncidentByIdQuery(999)));

        Assert.Contains("InTransitIncident", ex.Message);
    }

    [Fact]
    public async Task Maps_full_incident_details_including_order_cargo_driver_history_and_refund()
    {
        var sql = new FakeSql();

        var incidentTable = CreateIncidentTable();
        var now = new DateTime(2026, 10, 20, 8, 30, 0, DateTimeKind.Unspecified);
        incidentTable.Rows.Add(
            1L, 10L, "MMDEMO001", "InTransit", "VehicleBreakdown", "Nổ lốp xe", "[\"url1\",\"url2\"]",
            10.7769m, 106.7009m, "Resolved", "ContinueTrip", "Đã thay lốp phụ",
            4L, "Tài xế Demo", "Driver", 2L, "Dispatcher ModMove", now.AddMinutes(45), now,
            "12 Nguyễn Huệ, Q1", "45 Lê Văn Việt, Thủ Đức", "Cash", "Paid", 180000m, DBNull.Value,
            "Gia dụng", 2, 50m, 60, 40, 30, "Hàng dễ vỡ",
            3L, "Khách hàng Demo", "0900000003",
            1L, 4L, "Tài xế Demo", "0900000004", "51C-123.45", "Xe tải 1 tấn");
        sql.Results.Enqueue(incidentTable);

        var historyTable = CreateHistoriesTable();
        historyTable.Rows.Add(101L, DBNull.Value, "Pending", "Customer", 3L, "Khách hàng Demo", "Tạo đơn", now.AddHours(-2));
        historyTable.Rows.Add(102L, "Pending", "Accepted", "Driver", 4L, "Tài xế Demo", "Nhận đơn", now.AddHours(-1));
        sql.Results.Enqueue(historyTable);

        var refundTable = CreateRefundTable();
        refundTable.Rows.Add(501L, "Incident", 50000m, 30000m, "Requested", now.AddMinutes(30));
        sql.Results.Enqueue(refundTable);

        var res = await SenderWith(sql).Send(new GetIncidentByIdQuery(1));

        Assert.Equal(1, res.Id);
        Assert.Equal(10, res.OrderId);
        Assert.Equal("MMDEMO001", res.OrderCode);
        Assert.Equal("InTransit", res.OrderStatus);
        Assert.Equal("VehicleBreakdown", res.IncidentType);
        Assert.Equal("Nổ lốp xe", res.Description);
        Assert.Equal(["url1", "url2"], res.PhotoUrls);
        Assert.Equal(10.7769m, res.Latitude);
        Assert.Equal(106.7009m, res.Longitude);
        Assert.Equal("Resolved", res.ResolutionStatus);
        Assert.Equal("ContinueTrip", res.ResolutionDecision);
        Assert.Equal("Đã thay lốp phụ", res.ResolutionNote);
        Assert.Equal(4, res.ReportedById);
        Assert.Equal("Tài xế Demo", res.ReporterName);
        Assert.Equal("Driver", res.ReporterRole);
        Assert.Equal(2, res.HandledById);
        Assert.Equal("Dispatcher ModMove", res.HandledByName);
        Assert.NotNull(res.ResolvedAt);
        Assert.Equal(TimeSpan.Zero, res.ResolvedAt!.Value.Offset);

        // Order & Cargo
        Assert.Equal("12 Nguyễn Huệ, Q1", res.Order.PickupAddress);
        Assert.Equal("45 Lê Văn Việt, Thủ Đức", res.Order.DropoffAddress);
        Assert.Equal("Cash", res.Order.PaymentMethod);
        Assert.Equal("Paid", res.Order.PaymentStatus);
        Assert.Equal(180000m, res.Order.TotalFee);
        Assert.NotNull(res.Order.Cargo);
        Assert.Equal("Gia dụng", res.Order.Cargo!.CargoType);
        Assert.Equal(2, res.Order.Cargo.Quantity);
        Assert.Equal(50m, res.Order.Cargo.WeightKg);

        // Customer & Driver
        Assert.Equal("Khách hàng Demo", res.Customer.FullName);
        Assert.Equal("0900000003", res.Customer.PhoneNumber);
        Assert.NotNull(res.Driver);
        Assert.Equal(1, res.Driver!.DriverProfileId);
        Assert.Equal("51C-123.45", res.Driver.LicensePlate);
        Assert.Equal("Xe tải 1 tấn", res.Driver.VehicleTypeName);

        // Histories & Refund
        Assert.Equal(2, res.StatusHistories.Count);
        Assert.Equal("Pending", res.StatusHistories[0].ToStatus);
        Assert.Equal("Accepted", res.StatusHistories[1].ToStatus);
        Assert.NotNull(res.Refund);
        Assert.Equal(501, res.Refund!.Id);
        Assert.Equal(50000m, res.Refund.RequestedAmount);
        Assert.Equal(30000m, res.Refund.ProposedAmount);
    }

    [Fact]
    public async Task Handles_missing_driver_and_missing_refund()
    {
        var sql = new FakeSql();

        var incidentTable = CreateIncidentTable();
        var now = new DateTime(2026, 10, 20, 8, 30, 0, DateTimeKind.Unspecified);
        incidentTable.Rows.Add(
            2L, 20L, "MMDEMO002", "Pending", "Other", "Khách báo sự cố", DBNull.Value,
            DBNull.Value, DBNull.Value, "Open", DBNull.Value, DBNull.Value,
            3L, "Khách hàng Demo", "Customer", DBNull.Value, DBNull.Value, DBNull.Value, now,
            "A", "B", "PayOS", "Unpaid", 100000m, DBNull.Value,
            DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value,
            3L, "Khách hàng Demo", "0900000003",
            DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value);
        sql.Results.Enqueue(incidentTable);

        var historyTable = CreateHistoriesTable();
        sql.Results.Enqueue(historyTable);

        var refundTable = CreateRefundTable();
        sql.Results.Enqueue(refundTable);

        var res = await SenderWith(sql).Send(new GetIncidentByIdQuery(2));

        Assert.Equal(2, res.Id);
        Assert.Empty(res.PhotoUrls);
        Assert.Null(res.Driver);
        Assert.Null(res.Order.Cargo);
        Assert.Null(res.Refund);
        Assert.Empty(res.StatusHistories);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validates_id_must_be_positive(long id)
    {
        var sql = new FakeSql();

        await Assert.ThrowsAsync<ValidationException>(
            () => SenderWith(sql).Send(new GetIncidentByIdQuery(id)));
    }
}
