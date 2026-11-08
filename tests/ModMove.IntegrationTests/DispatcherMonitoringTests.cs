using System.Data;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ModMove.Application;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Features.Monitoring.Queries.GetDispatcherOrderById;
using ModMove.Application.Features.Monitoring.Queries.GetDispatcherOrdersSummary;
using ModMove.Application.Features.Monitoring.Queries.ListDispatcherOrders;
using ModMove.Application.Features.Monitoring.Queries.ListHandledCases;
using ModMove.Application.Features.Monitoring.Queries.ListOnlineDrivers;
using ModMove.Domain.Enums;

namespace ModMove.IntegrationTests;

/// <summary>
/// UC-DIS-01 &amp; UC-DIS-16: Dispatcher delivery monitoring (order list with attention flags, summary counts,
/// full order details, online drivers map) and my handled cases through the real MediatR pipeline with <see cref="FakeSql"/>.
/// </summary>
public class DispatcherMonitoringTests
{
    private static readonly DateTimeOffset Now = TestApp.Start;

    private static ISender SenderWith(FakeSql sql, long? userId = 10L) =>
        new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddSingleton<ISqlConnectionFactory>(sql)
            .AddSingleton<ICurrentUserService>(new FakeCurrentUser(userId))
            .AddSingleton<TimeProvider>(new MutableTime(Now))
            .BuildServiceProvider()
            .GetRequiredService<ISender>();

    private static DataTable ActiveAccountTable(string status = "Active")
    {
        var t = new DataTable();
        t.Columns.Add("AccountStatus", typeof(string));
        t.Rows.Add(status);
        return t;
    }

    [Fact]
    public async Task ListOrders_ComputesMatchingTimeoutSoon_ForCashAndPaidPayOs_AndAwaitingPickupApproval()
    {
        var sql = new FakeSql();

        var table = new DataTable();
        table.Columns.Add("Id", typeof(long));
        table.Columns.Add("OrderCode", typeof(string));
        table.Columns.Add("OrderStatus", typeof(string));
        table.Columns.Add("PaymentMethod", typeof(string));
        table.Columns.Add("PaymentStatus", typeof(string));
        table.Columns.Add("TotalFee", typeof(decimal));
        table.Columns.Add("PickupAddress", typeof(string));
        table.Columns.Add("DropoffAddress", typeof(string));
        table.Columns.Add("CustomerName", typeof(string));
        table.Columns.Add("CustomerPhone", typeof(string));
        table.Columns.Add("DriverProfileId", typeof(long));
        table.Columns.Add("DriverName", typeof(string));
        table.Columns.Add("DriverPhone", typeof(string));
        table.Columns.Add("LicensePlate", typeof(string));
        table.Columns.Add("VehicleTypeName", typeof(string));
        table.Columns.Add("ProofStatus", typeof(string));
        table.Columns.Add("OpenIncidentId", typeof(long));
        table.Columns.Add("CreatedAt", typeof(DateTime));
        table.Columns.Add("PaidAt", typeof(DateTime));
        table.Columns.Add("TotalCount", typeof(long));

        // 1. Pending Cash order created 9 minutes ago => MatchingTimeoutSoon = true
        table.Rows.Add(1L, "MM001", "Pending", "Cash", "Unpaid", 150_000m, "A", "B", "Khách 1", "0901111111",
            DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, "Xe 500kg", DBNull.Value, DBNull.Value,
            Now.AddMinutes(-9).UtcDateTime, DBNull.Value, 3L);

        // 2. Pending PayOS order created 15 minutes ago, UNPAID => MatchingTimeoutSoon = false (DECISIONS 3.2: timer starts at PaidAt)
        table.Rows.Add(2L, "MM002", "Pending", "PayOS", "Unpaid", 200_000m, "A", "B", "Khách 2", "0902222222",
            DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, "Xe 500kg", DBNull.Value, DBNull.Value,
            Now.AddMinutes(-15).UtcDateTime, DBNull.Value, 3L);

        // 3. PickedUp order with Pending proof and an open incident => AwaitingPickupApproval = true, HasOpenIncident = true
        table.Rows.Add(3L, "MM003", "PickedUp", "PayOS", "Paid", 250_000m, "A", "B", "Khách 3", "0903333333",
            5L, "Tài Xế", "0909999999", "29A-12345", "Xe 1 tấn", "Pending", 42L,
            Now.AddMinutes(-30).UtcDateTime, Now.AddMinutes(-28).UtcDateTime, 3L);

        sql.Results.Enqueue(table);

        var res = await SenderWith(sql).Send(new ListDispatcherOrdersQuery(Search: "MM00"));

        Assert.Equal(3, res.TotalCount);
        Assert.Equal("%MM00%", sql.LastParameters["SearchPattern"]);

        Assert.True(res.Items[0].MatchingTimeoutSoon);
        Assert.False(res.Items[0].AwaitingPickupApproval);

        Assert.False(res.Items[1].MatchingTimeoutSoon);

        Assert.False(res.Items[2].MatchingTimeoutSoon);
        Assert.True(res.Items[2].AwaitingPickupApproval);
        Assert.Equal(42L, res.Items[2].OpenIncidentId);
    }

    [Fact]
    public async Task GetOrdersSummary_ReturnsCountsForAllEightStatuses_DefaultingMissingOnesToZero()
    {
        var sql = new FakeSql();
        var t = new DataTable();
        t.Columns.Add("OrderStatus", typeof(string));
        t.Columns.Add("Count", typeof(long));
        t.Rows.Add("Pending", 4L);
        t.Rows.Add("InTransit", 7L);
        t.Rows.Add("Completed", 12L);
        sql.Results.Enqueue(t);

        var summary = await SenderWith(sql).Send(new GetDispatcherOrdersSummaryQuery());

        Assert.Equal(23, summary.Total);
        Assert.Equal(4, summary.Pending);
        Assert.Equal(0, summary.Accepted);
        Assert.Equal(0, summary.Arrived);
        Assert.Equal(0, summary.PickedUp);
        Assert.Equal(7, summary.InTransit);
        Assert.Equal(0, summary.Delivered);
        Assert.Equal(12, summary.Completed);
        Assert.Equal(0, summary.Cancelled);
    }

    [Fact]
    public async Task GetOrderById_ReturnsFullMonitoringDetails_WithTimelinePaymentsIncidentsAndRefunds()
    {
        var sql = new FakeSql();

        var orderTable = new DataTable();
        orderTable.Columns.Add("Id", typeof(long));
        orderTable.Columns.Add("OrderCode", typeof(string));
        orderTable.Columns.Add("OrderStatus", typeof(string));
        orderTable.Columns.Add("VehicleTypeId", typeof(long));
        orderTable.Columns.Add("VehicleTypeName", typeof(string));
        orderTable.Columns.Add("PickupAddress", typeof(string));
        orderTable.Columns.Add("PickupLatitude", typeof(decimal));
        orderTable.Columns.Add("PickupLongitude", typeof(decimal));
        orderTable.Columns.Add("DropoffAddress", typeof(string));
        orderTable.Columns.Add("DropoffLatitude", typeof(decimal));
        orderTable.Columns.Add("DropoffLongitude", typeof(decimal));
        orderTable.Columns.Add("ReceiverName", typeof(string));
        orderTable.Columns.Add("ReceiverPhone", typeof(string));
        orderTable.Columns.Add("EstimatedDistanceKm", typeof(decimal));
        orderTable.Columns.Add("EstimatedDurationMin", typeof(int));
        orderTable.Columns.Add("HasLoadingService", typeof(bool));
        orderTable.Columns.Add("DistanceFee", typeof(decimal));
        orderTable.Columns.Add("ServiceFee", typeof(decimal));
        orderTable.Columns.Add("DiscountAmount", typeof(decimal));
        orderTable.Columns.Add("TotalFee", typeof(decimal));
        orderTable.Columns.Add("PaymentMethod", typeof(string));
        orderTable.Columns.Add("PayerRole", typeof(string));
        orderTable.Columns.Add("PaymentStatus", typeof(string));
        orderTable.Columns.Add("CancellationReason", typeof(string));
        orderTable.Columns.Add("CommissionRate", typeof(decimal));
        orderTable.Columns.Add("CommissionAmount", typeof(decimal));
        orderTable.Columns.Add("CreatedAt", typeof(DateTime));
        orderTable.Columns.Add("PaidAt", typeof(DateTime));
        orderTable.Columns.Add("AcceptedAt", typeof(DateTime));
        orderTable.Columns.Add("DeliveredAt", typeof(DateTime));
        orderTable.Columns.Add("CompletedAt", typeof(DateTime));
        orderTable.Columns.Add("CustomerId", typeof(long));
        orderTable.Columns.Add("CustomerFullName", typeof(string));
        orderTable.Columns.Add("CustomerPhone", typeof(string));
        orderTable.Columns.Add("CustomerEmail", typeof(string));
        orderTable.Columns.Add("DriverProfileId", typeof(long));
        orderTable.Columns.Add("DriverUserId", typeof(long));
        orderTable.Columns.Add("DriverFullName", typeof(string));
        orderTable.Columns.Add("DriverPhone", typeof(string));
        orderTable.Columns.Add("LicensePlate", typeof(string));
        orderTable.Columns.Add("DriverLastLatitude", typeof(decimal));
        orderTable.Columns.Add("DriverLastLongitude", typeof(decimal));
        orderTable.Columns.Add("DriverLastLocationAt", typeof(DateTime));
        orderTable.Columns.Add("CargoType", typeof(string));
        orderTable.Columns.Add("CargoTypeName", typeof(string));
        orderTable.Columns.Add("CargoQuantity", typeof(int));
        orderTable.Columns.Add("CargoWeightKg", typeof(decimal));
        orderTable.Columns.Add("CargoLengthCm", typeof(int));
        orderTable.Columns.Add("CargoWidthCm", typeof(int));
        orderTable.Columns.Add("CargoHeightCm", typeof(int));
        orderTable.Columns.Add("CargoPhotoUrlsJson", typeof(string));
        orderTable.Columns.Add("CargoSpecialNotes", typeof(string));
        orderTable.Columns.Add("PickupPhotoUrlsJson", typeof(string));
        orderTable.Columns.Add("ProofStatus", typeof(string));
        orderTable.Columns.Add("PickupSubmissionCount", typeof(int));
        orderTable.Columns.Add("RejectionReason", typeof(string));
        orderTable.Columns.Add("DeliveryPhotoUrl", typeof(string));
        orderTable.Columns.Add("DeliveryCompletionTime", typeof(DateTime));

        orderTable.Rows.Add(
            55L, "MM20261020055", "InTransit", 1L, "Xe 500kg",
            "10 Cầu Giấy", 21.03m, 105.80m, "20 Hoàn Kiếm", 21.02m, 105.85m,
            "Người Nhận", "0911111111", 8.5m, 25, true,
            150_000m, 50_000m, 10_000m, 190_000m,
            "PayOS", DBNull.Value, "Paid", DBNull.Value, DBNull.Value, DBNull.Value,
            Now.AddMinutes(-40).UtcDateTime, Now.AddMinutes(-38).UtcDateTime, Now.AddMinutes(-35).UtcDateTime, DBNull.Value, DBNull.Value,
            2L, "Nguyễn Văn Khách", "0901234567", "khach@x.vn",
            3L, 30L, "Trần Tài Xế", "0907654321", "29A-88888", 21.025m, 105.825m, Now.AddMinutes(-1).UtcDateTime,
            "FURNITURE", "Đồ nội thất", 2, 120m, 150, 80, 90, "[\"https://res.cloudinary.com/demo/c1.jpg\"]", "Hàng dễ vỡ",
            "[\"https://res.cloudinary.com/demo/p1.jpg\",\"https://res.cloudinary.com/demo/p2.jpg\"]", "Verified", 1, DBNull.Value, DBNull.Value, DBNull.Value);
        sql.Results.Enqueue(orderTable);

        var historyTable = new DataTable();
        historyTable.Columns.Add("Id", typeof(long));
        historyTable.Columns.Add("FromStatus", typeof(string));
        historyTable.Columns.Add("ToStatus", typeof(string));
        historyTable.Columns.Add("ChangedByRole", typeof(string));
        historyTable.Columns.Add("ChangedByUserId", typeof(long));
        historyTable.Columns.Add("ChangedByUserName", typeof(string));
        historyTable.Columns.Add("Note", typeof(string));
        historyTable.Columns.Add("CreatedAt", typeof(DateTime));
        historyTable.Rows.Add(1L, DBNull.Value, "Pending", "Customer", 2L, "Nguyễn Văn Khách", DBNull.Value, Now.AddMinutes(-40).UtcDateTime);
        sql.Results.Enqueue(historyTable);

        var paymentTable = new DataTable();
        paymentTable.Columns.Add("Id", typeof(long));
        paymentTable.Columns.Add("PaymentMethod", typeof(string));
        paymentTable.Columns.Add("PayerRole", typeof(string));
        paymentTable.Columns.Add("Amount", typeof(decimal));
        paymentTable.Columns.Add("PayosOrderCode", typeof(long));
        paymentTable.Columns.Add("TransactionStatus", typeof(string));
        paymentTable.Columns.Add("PaidAt", typeof(DateTime));
        paymentTable.Columns.Add("CreatedAt", typeof(DateTime));
        paymentTable.Rows.Add(9L, "PayOS", DBNull.Value, 190_000m, 999001L, "Paid", Now.AddMinutes(-38).UtcDateTime, Now.AddMinutes(-39).UtcDateTime);
        sql.Results.Enqueue(paymentTable);

        var incidentTable = new DataTable();
        incidentTable.Columns.Add("Id", typeof(long));
        incidentTable.Columns.Add("IncidentType", typeof(string));
        incidentTable.Columns.Add("Description", typeof(string));
        incidentTable.Columns.Add("ResolutionStatus", typeof(string));
        incidentTable.Columns.Add("ResolutionDecision", typeof(string));
        incidentTable.Columns.Add("ResolutionNote", typeof(string));
        incidentTable.Columns.Add("ReporterName", typeof(string));
        incidentTable.Columns.Add("HandledByName", typeof(string));
        incidentTable.Columns.Add("CreatedAt", typeof(DateTime));
        incidentTable.Columns.Add("ResolvedAt", typeof(DateTime));
        incidentTable.Rows.Add(7L, "VehicleBreakdown", "Xịt lốp", "Open", DBNull.Value, DBNull.Value, "Trần Tài Xế", DBNull.Value, Now.AddMinutes(-5).UtcDateTime, DBNull.Value);
        sql.Results.Enqueue(incidentTable);

        var refundTable = new DataTable();
        refundTable.Columns.Add("Id", typeof(long));
        refundTable.Columns.Add("IncidentId", typeof(long));
        refundTable.Columns.Add("SourceType", typeof(string));
        refundTable.Columns.Add("Reason", typeof(string));
        refundTable.Columns.Add("RequestedAmount", typeof(decimal));
        refundTable.Columns.Add("ProposedAmount", typeof(decimal));
        refundTable.Columns.Add("RefundStatus", typeof(string));
        refundTable.Columns.Add("CreatedAt", typeof(DateTime));
        refundTable.Columns.Add("DecidedAt", typeof(DateTime));
        refundTable.Columns.Add("ProcessedAt", typeof(DateTime));
        sql.Results.Enqueue(refundTable);

        var detail = await SenderWith(sql).Send(new GetDispatcherOrderByIdQuery(55));

        Assert.Equal("MM20261020055", detail.OrderCode);
        Assert.Equal("Nguyễn Văn Khách", detail.Customer.FullName);
        Assert.Equal("29A-88888", detail.Driver!.LicensePlate);
        Assert.Equal("FURNITURE", detail.Cargo!.CargoType);
        Assert.Equal("Đồ nội thất", detail.Cargo.CargoTypeName);
        Assert.Single(detail.Cargo!.PhotoUrls);
        Assert.Equal(2, detail.Proof!.PickupPhotoUrls.Count);
        Assert.Single(detail.StatusHistories);
        Assert.Single(detail.Payments);
        Assert.Single(detail.Incidents);
        Assert.Empty(detail.Refunds);
    }

    [Fact]
    public async Task ListOnlineDrivers_QueriesFresh5MinuteWindow_AndIncludesBothIdleAndBusyOnlineDrivers()
    {
        var sql = new FakeSql();

        var t = new DataTable();
        t.Columns.Add("DriverProfileId", typeof(long));
        t.Columns.Add("UserId", typeof(long));
        t.Columns.Add("FullName", typeof(string));
        t.Columns.Add("PhoneNumber", typeof(string));
        t.Columns.Add("LicensePlate", typeof(string));
        t.Columns.Add("VehicleTypeId", typeof(long));
        t.Columns.Add("VehicleTypeName", typeof(string));
        t.Columns.Add("Latitude", typeof(decimal));
        t.Columns.Add("Longitude", typeof(decimal));
        t.Columns.Add("LastLocationAt", typeof(DateTime));
        t.Columns.Add("ActiveOrderId", typeof(long));
        t.Columns.Add("ActiveOrderCode", typeof(string));
        t.Columns.Add("ActiveOrderStatus", typeof(string));

        t.Rows.Add(1L, 11L, "Tài Đang Chạy", "0901111111", "29A-11111", 1L, "Xe 500kg", 21.0285m, 105.8542m, Now.AddMinutes(-1).UtcDateTime, 100L, "MM001", "InTransit");
        t.Rows.Add(2L, 12L, "Tài Đang Rảnh", "0902222222", "29A-22222", 2L, "Xe 1 tấn", 21.0350m, 105.8400m, Now.AddMinutes(-2).UtcDateTime, DBNull.Value, DBNull.Value, DBNull.Value);
        sql.Results.Enqueue(t);

        var drivers = await SenderWith(sql).Send(new ListOnlineDriversQuery());

        Assert.Equal(2, drivers.Count);
        Assert.Equal(100L, drivers[0].ActiveOrderId);
        Assert.Null(drivers[1].ActiveOrderId);
        Assert.Equal(Now.AddMinutes(-5).UtcDateTime, sql.LastParameters["FreshSince"]);
        Assert.Contains("last_location_at > @FreshSince", sql.LastSql); // exactly 5 minutes old is disconnected (6.4)
    }

    [Fact]
    public async Task ListHandledCases_BindsCurrentDispatcherId_AndRefusesLockedDispatcher()
    {
        var lockedSql = new FakeSql();
        lockedSql.Results.Enqueue(ActiveAccountTable("Locked"));

        var forbidden = await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            SenderWith(lockedSql, userId: 77L).Send(new ListHandledCasesQuery()));
        Assert.Equal("ACCOUNT_INACTIVE", forbidden.Code);

        var sql = new FakeSql();
        sql.Results.Enqueue(ActiveAccountTable("Active"));

        var cases = new DataTable();
        cases.Columns.Add("CaseKind", typeof(string));
        cases.Columns.Add("CaseId", typeof(long));
        cases.Columns.Add("OrderId", typeof(long));
        cases.Columns.Add("OrderCode", typeof(string));
        cases.Columns.Add("SubType", typeof(string));
        cases.Columns.Add("Status", typeof(string));
        cases.Columns.Add("Decision", typeof(string));
        cases.Columns.Add("Note", typeof(string));
        cases.Columns.Add("RequestedAmount", typeof(decimal));
        cases.Columns.Add("ProposedAmount", typeof(decimal));
        cases.Columns.Add("HandledAt", typeof(DateTime));
        cases.Columns.Add("TotalCount", typeof(long));

        cases.Rows.Add("Incident", 12L, 100L, "MM100", "VehicleBreakdown", "Resolved", "ReassignDriver", "Đã đổi xe", DBNull.Value, DBNull.Value, Now.AddMinutes(-10).UtcDateTime, 2L);
        cases.Rows.Add("Refund", 19L, 101L, "MM101", "Complaint", "Approved", DBNull.Value, "Bồi thường một phần", 200_000m, 100_000m, Now.AddMinutes(-20).UtcDateTime, 2L);
        sql.Results.Enqueue(cases);

        var res = await SenderWith(sql, userId: 77L).Send(new ListHandledCasesQuery(Type: null, Page: 1, PageSize: 20));

        Assert.Equal(2, res.TotalCount);
        Assert.Equal(77L, sql.LastParameters["DispatcherId"]);
        Assert.Equal("Incident", res.Items[0].CaseKind);
        Assert.Equal("ReassignDriver", res.Items[0].Decision);
        Assert.Equal("Refund", res.Items[1].CaseKind);
        Assert.Equal(100_000m, res.Items[1].ProposedAmount);
    }
}

