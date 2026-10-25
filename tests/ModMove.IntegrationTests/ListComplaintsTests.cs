using System.Data;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ModMove.Application;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Features.Refunds.Queries.ListComplaints;
using ModMove.Domain.Enums;

namespace ModMove.IntegrationTests;

/// <summary>GET api/dispatcher/complaints (UC-DIS-11) through the real pipeline with <see cref="FakeSql"/>.</summary>
public class ListComplaintsTests
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
        table.Columns.Add("CustomerName", typeof(string));
        table.Columns.Add("Reason", typeof(string));
        table.Columns.Add("RequestedAmount", typeof(decimal));
        table.Columns.Add("ProposedAmount", typeof(decimal));
        table.Columns.Add("Stage", typeof(string));
        table.Columns.Add("RefundStatus", typeof(string));
        table.Columns.Add("DeliveredAt", typeof(DateTime));
        table.Columns.Add("CreatedAt", typeof(DateTime));
        table.Columns.Add("TotalCount", typeof(int));
        return table;
    }

    private static readonly DateTime Delivered = new(2026, 10, 20, 10, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime Created = new(2026, 10, 20, 12, 30, 0, DateTimeKind.Unspecified);

    [Fact]
    public async Task Rows_become_a_page_with_inferred_stage_and_total()
    {
        var table = NewTable();
        table.Rows.Add(
            1L, 10L, "MMDEMO006", "Khách hàng Demo", "Hàng trầy xước", 240000m, DBNull.Value,
            "NeedsReview", "Requested", Delivered, Created, 5);
        table.Rows.Add(
            2L, 11L, "MMDEMO007", "Khách hàng Demo", "Thiếu hàng", 200000m, DBNull.Value,
            "WaitingForCustomer", "Requested", Delivered, Created.AddHours(1), 5);
        var sql = new FakeSql { Result = table };

        var page = await SenderWith(sql).Send(new ListComplaintsQuery(null, null, 1, 10));

        Assert.Equal(1, page.PageNumber);
        Assert.Equal(10, page.PageSize);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(2, page.Items.Count);

        var first = page.Items[0];
        Assert.Equal(1, first.Id);
        Assert.Equal(10, first.OrderId);
        Assert.Equal("MMDEMO006", first.OrderCode);
        Assert.Equal("Khách hàng Demo", first.CustomerName);
        Assert.Equal("Hàng trầy xước", first.Reason);
        Assert.Equal(240000m, first.RequestedAmount);
        Assert.Null(first.ProposedAmount);
        Assert.Equal("NeedsReview", first.Stage);
        Assert.Equal("Requested", first.RefundStatus);
        Assert.NotNull(first.DeliveredAt);
        Assert.Equal(TimeSpan.Zero, first.DeliveredAt!.Value.Offset);
        Assert.Equal(TimeSpan.Zero, first.CreatedAt.Offset);

        Assert.Equal("WaitingForCustomer", page.Items[1].Stage);
    }

    [Fact]
    public async Task Filters_and_paging_are_bound_as_parameters()
    {
        var table = NewTable();
        table.Rows.Add(
            1L, 10L, "MMDEMO006", "Khách hàng Demo", "Lý do", 100000m, DBNull.Value,
            "NeedsReview", "Requested", Delivered, Created, 1);
        var sql = new FakeSql { Result = table };

        await SenderWith(sql).Send(new ListComplaintsQuery(
            ComplaintStage.NeedsReview, "MM_006%", Page: 2, PageSize: 5));

        Assert.Equal("NeedsReview", sql.LastParameters["Stage"]);
        Assert.Equal(@"%MM\_006\%%", sql.LastParameters["SearchPattern"]);
        Assert.Equal(5, sql.LastParameters["PageSize"]);
        Assert.Equal(5, sql.LastParameters["Offset"]);
    }

    [Fact]
    public async Task No_filter_binds_DBNull_and_only_queries_complaint_source()
    {
        var table = NewTable();
        var sql = new FakeSql { Result = table };

        await SenderWith(sql).Send(new ListComplaintsQuery(null, null));

        Assert.Equal(DBNull.Value, sql.LastParameters["Stage"]);
        Assert.Equal(DBNull.Value, sql.LastParameters["SearchPattern"]);
        Assert.Contains("r.source_type = 'Complaint'", sql.LastSql);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Validates_page_and_pageSize(int page, int pageSize)
    {
        var sql = new FakeSql { Result = NewTable() };

        await Assert.ThrowsAsync<ValidationException>(
            () => SenderWith(sql).Send(new ListComplaintsQuery(null, null, page, pageSize)));
    }
}
