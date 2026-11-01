using System.Data;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ModMove.Application;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Features.Incidents.Queries.ListIncidents;
using ModMove.Domain.Enums;

namespace ModMove.IntegrationTests;

/// <summary>GET api/dispatcher/incidents (UC-DIS-03) through the real pipeline, with rows served by <see cref="FakeSql"/>.</summary>
public class ListIncidentsTests
{
    private static ISender SenderWith(FakeSql sql) => new ServiceCollection()
        .AddLogging()
        .AddApplication()
        .AddSingleton<ISqlConnectionFactory>(sql)
        .BuildServiceProvider()
        .GetRequiredService<ISender>();

    private static DataTable NewTable()
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(long));
        table.Columns.Add("OrderId", typeof(long));
        table.Columns.Add("OrderCode", typeof(string));
        table.Columns.Add("OrderStatus", typeof(string));
        table.Columns.Add("IncidentType", typeof(string));
        table.Columns.Add("Description", typeof(string));
        table.Columns.Add("ReportedById", typeof(long));
        table.Columns.Add("ReporterName", typeof(string));
        table.Columns.Add("ReporterRole", typeof(string));
        table.Columns.Add("ResolutionStatus", typeof(string));
        table.Columns.Add("HandledById", typeof(long));
        table.Columns.Add("HandledByName", typeof(string));
        table.Columns.Add("CreatedAt", typeof(DateTime));
        table.Columns.Add("TotalCount", typeof(int));
        return table;
    }

    private static readonly DateTime Created = new(2026, 10, 20, 8, 30, 0, DateTimeKind.Unspecified);

    [Fact]
    public async Task Rows_become_a_page_with_the_total_from_the_query()
    {
        var table = NewTable();
        table.Rows.Add(5L, 11L, "MMDEMO001", "InTransit", "VehicleBreakdown", "Nổ lốp", 4L, "Tài xế Demo", "Driver",
            "Open", DBNull.Value, DBNull.Value, Created, 42);
        table.Rows.Add(6L, 12L, "MMDEMO002", "PickedUp", "DamagedCargo", "Hàng móp", 3L, "Khách hàng Demo", "Customer",
            "InProgress", 2L, "Dispatcher ModMove", Created.AddHours(1), 42);
        var sql = new FakeSql { Result = table };

        var page = await SenderWith(sql).Send(new ListIncidentsQuery(null, null, null, 2, 2));

        Assert.Equal(2, page.PageNumber);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(42, page.TotalCount);
        Assert.Equal(2, page.Items.Count);

        var first = page.Items[0];
        Assert.Equal(5, first.Id);
        Assert.Equal(11, first.OrderId);
        Assert.Equal("MMDEMO001", first.OrderCode);
        Assert.Equal("InTransit", first.OrderStatus);
        Assert.Equal("VehicleBreakdown", first.IncidentType);
        Assert.Equal("Tài xế Demo", first.ReporterName);
        Assert.Equal("Driver", first.ReporterRole);
        Assert.Equal("Open", first.ResolutionStatus);
        Assert.Null(first.HandledById);
        Assert.Null(first.HandledByName);

        Assert.Equal("Dispatcher ModMove", page.Items[1].HandledByName);
    }

    // The database stores UTC without a zone; the API must say it is UTC (ARCHITECTURE: ISO 8601, UTC).
    [Fact]
    public async Task Created_time_is_reported_as_UTC()
    {
        var table = NewTable();
        table.Rows.Add(5L, 11L, "MMDEMO001", "InTransit", "Other", "x", 4L, "A", "Driver", "Open", DBNull.Value, DBNull.Value, Created, 1);

        var page = await SenderWith(new FakeSql { Result = table }).Send(new ListIncidentsQuery(null, null, null));

        Assert.Equal(new DateTimeOffset(2026, 10, 20, 8, 30, 0, TimeSpan.Zero), page.Items[0].CreatedAt);
        Assert.Equal(TimeSpan.Zero, page.Items[0].CreatedAt.Offset);
    }

    [Fact]
    public async Task Filters_and_paging_are_bound_as_parameters_not_pasted_into_the_SQL()
    {
        // A non-empty page, so no fallback count query replaces the page query as the last one run.
        var table = NewTable();
        table.Rows.Add(5L, 11L, "MM_20", "InTransit", "Accident", "x", 4L, "A", "Driver", "Open", DBNull.Value, DBNull.Value, Created, 11);
        var sql = new FakeSql { Result = table };

        await SenderWith(sql).Send(new ListIncidentsQuery(
            IncidentResolutionStatus.Open, IncidentType.Accident, "MM_20%", Page: 3, PageSize: 10));

        Assert.Equal("Open", sql.LastParameters["Status"]);
        Assert.Equal("Accident", sql.LastParameters["IncidentType"]);
        Assert.Equal(10, sql.LastParameters["PageSize"]);
        Assert.Equal(20, sql.LastParameters["Offset"]);
        // % and _ typed by the user are literal characters, not wildcards.
        Assert.Equal(@"%MM\_20\%%", sql.LastParameters["SearchPattern"]);
        Assert.DoesNotContain("MM_20", sql.LastSql);
    }

    [Fact]
    public async Task No_filter_binds_null_so_every_status_is_listed()
    {
        var sql = new FakeSql { Result = NewTable() };

        await SenderWith(sql).Send(new ListIncidentsQuery(null, null, null));

        // Dapper sends a null filter as DBNull, which the SQL reads as "no filter".
        Assert.Equal(DBNull.Value, sql.LastParameters["Status"]);
        Assert.Equal(DBNull.Value, sql.LastParameters["IncidentType"]);
        Assert.Equal(DBNull.Value, sql.LastParameters["SearchPattern"]);
        Assert.Equal(0, sql.LastParameters["Offset"]);
        Assert.Equal(20, sql.LastParameters["PageSize"]);
    }

    // The queue shows what has waited longest first: Open, then InProgress, then closed; oldest first inside each.
    [Fact]
    public async Task Open_incidents_come_first_and_the_oldest_leads_each_group()
    {
        var sql = new FakeSql { Result = NewTable() };

        await SenderWith(sql).Send(new ListIncidentsQuery(null, null, null));

        var order = sql.LastSql[sql.LastSql.IndexOf("ORDER BY", StringComparison.Ordinal)..];
        var open = order.IndexOf("'Open' THEN 0", StringComparison.Ordinal);
        var inProgress = order.IndexOf("'InProgress' THEN 1", StringComparison.Ordinal);
        Assert.True(open >= 0 && inProgress > open);
        Assert.Contains("ELSE 2", order);
        Assert.Contains("i.created_at", order);
        Assert.DoesNotContain("DESC", order);
    }

    [Fact]
    public async Task Empty_result_is_an_empty_page_with_a_zero_total()
    {
        var page = await SenderWith(new FakeSql { Result = NewTable() }).Send(new ListIncidentsQuery(null, null, null));

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    // No row on a page past the end to carry the total, so the handler asks for it with a second query.
    [Fact]
    public async Task A_page_past_the_end_counts_the_incidents_with_the_same_filters()
    {
        var sql = new FakeSql { Result = NewTable() };

        var page = await SenderWith(sql).Send(new ListIncidentsQuery(IncidentResolutionStatus.Open, null, null, Page: 9));

        Assert.Empty(page.Items);
        Assert.StartsWith("SELECT COUNT(*)", sql.LastSql);
        Assert.Contains("i.resolution_status = @Status", sql.LastSql);
        Assert.Equal("Open", sql.LastParameters["Status"]);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Page_and_page_size_must_be_in_range(int page, int pageSize)
    {
        var sql = new FakeSql { Result = NewTable() };

        await Assert.ThrowsAsync<ModMove.Application.Common.Exceptions.ValidationException>(
            () => SenderWith(sql).Send(new ListIncidentsQuery(null, null, null, page, pageSize)));
    }

    [Fact]
    public async Task Search_longer_than_an_order_code_is_refused()
    {
        var sql = new FakeSql { Result = NewTable() };

        await Assert.ThrowsAsync<ModMove.Application.Common.Exceptions.ValidationException>(
            () => SenderWith(sql).Send(new ListIncidentsQuery(null, null, new string('M', 21))));
    }
}
