using System.Data;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ModMove.Application;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Features.Refunds.Queries.GetMyRefundDetail;
using ModMove.Application.Features.Refunds.Queries.GetMyRefunds;

namespace ModMove.IntegrationTests;

/// <summary>GET api/customer/refunds and refunds/{id} (UC-CUS-16, 23); rows served by FakeSql.</summary>
public class MyRefundsQueryTests
{
    private const long CustomerId = 7;

    private sealed class Db
    {
        public FakeSql Sql { get; } = new();
        public string AccountStatus { get; set; } = "Active";
        public string Status { get; set; } = "Requested";
        public bool Found { get; set; } = true;
        public List<(string Sql, IReadOnlyDictionary<string, object?> Parameters)> Executed { get; } = [];

        public Db()
        {
            Sql.ScalarResult = 1;
            Sql.CustomQuery = (text, parameters) =>
            {
                Executed.Add((text, parameters));
                if (text.Contains("FROM users"))
                    return Table(("AccountStatus", typeof(string), AccountStatus));
                var row = Table(
                    ("Id", typeof(long), 4L), ("OrderId", typeof(long), 1L), ("OrderCode", typeof(string), "MM20261020001"),
                    ("IncidentId", typeof(long), null), ("SourceType", typeof(string), "Complaint"), ("RefundStatus", typeof(string), Status),
                    ("Reason", typeof(string), "Hàng vỡ"), ("EvidenceUrls", typeof(string), """["https://res.cloudinary.com/demo/a.jpg"]"""),
                    ("RequestedAmount", typeof(decimal), 100_000m), ("ProposedAmount", typeof(decimal), 80_000m),
                    ("InfoRequestNote", typeof(string), "Chụp thêm ảnh"), ("CustomerReply", typeof(string), null),
                    ("DecidedAt", typeof(DateTime), null), ("ProcessedAt", typeof(DateTime), null),
                    ("CreatedAt", typeof(DateTime), new DateTime(2026, 10, 20, 9, 0, 0)));
                if (!Found)
                    row.Rows.Clear();
                return row;
            };
        }

        public ISender Sender() => new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddSingleton<ISqlConnectionFactory>(Sql)
            .AddSingleton<ICurrentUserService>(new FakeCurrentUser(CustomerId))
            .BuildServiceProvider()
            .GetRequiredService<ISender>();

        public (string Sql, IReadOnlyDictionary<string, object?> Parameters) RefundsQuery() =>
            Executed.Last(e => e.Sql.Contains("FROM refunds"));
    }

    private static DataTable Table(params (string Name, Type Type, object? Value)[] columns)
    {
        var table = new DataTable();
        foreach (var c in columns)
            table.Columns.Add(c.Name, c.Type);
        table.Rows.Add(columns.Select(c => c.Value ?? DBNull.Value).ToArray());
        return table;
    }

    [Fact]
    public async Task List_reads_only_refunds_of_the_callers_orders()
    {
        var db = new Db();

        var page = await db.Sender().Send(new GetMyRefundsQuery());

        var (sql, parameters) = db.RefundsQuery();
        Assert.Contains("o.customer_id = @CustomerId", sql);
        Assert.Equal(CustomerId, parameters["CustomerId"]);
        var item = Assert.Single(page.Items);
        Assert.Equal((4L, "MM20261020001", "Complaint", "Requested"), (item.Id, item.OrderCode, item.SourceType, item.RefundStatus));
        Assert.True(item.WaitingForYourReply);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task Dispatcher_proposal_stays_hidden_until_the_admin_approves_it()
    {
        var requested = await new Db().Sender().Send(new GetMyRefundDetailQuery(4));
        var approved = await new Db { Status = "Approved" }.Sender().Send(new GetMyRefundDetailQuery(4));

        Assert.Null(requested.ApprovedAmount);
        Assert.Equal(80_000m, approved.ApprovedAmount);
    }

    [Fact]
    public async Task Detail_has_evidence_and_the_dispatchers_question()
    {
        var db = new Db();

        var refund = await db.Sender().Send(new GetMyRefundDetailQuery(4));

        var (sql, parameters) = db.RefundsQuery();
        Assert.Contains("o.customer_id = @CustomerId", sql);
        Assert.Equal(4L, parameters["RefundId"]);
        Assert.Equal(["https://res.cloudinary.com/demo/a.jpg"], refund.EvidenceUrls);
        Assert.Equal(("Chụp thêm ảnh", (string?)null), (refund.InfoRequestNote, refund.CustomerReply));
        Assert.Equal(100_000m, refund.RequestedAmount);
    }

    [Theory]
    [InlineData("Processed", 80_000)]
    [InlineData("Rejected", null)]
    public async Task Approved_amount_after_payout_and_none_when_rejected(string status, int? expected)
    {
        var refund = await new Db { Status = status }.Sender().Send(new GetMyRefundDetailQuery(4));

        Assert.Equal((decimal?)expected, refund.ApprovedAmount);
        Assert.False(refund.WaitingForYourReply);
    }

    [Fact]
    public async Task Someone_elses_refund_is_404() =>
        await Assert.ThrowsAsync<NotFoundException>(() => new Db { Found = false }.Sender().Send(new GetMyRefundDetailQuery(4)));

    [Fact]
    public async Task Locked_account_is_refused_B8()
    {
        var error = await Assert.ThrowsAsync<ForbiddenAccessException>(() => new Db { AccountStatus = "Locked" }.Sender().Send(new GetMyRefundsQuery()));

        Assert.Equal("ACCOUNT_INACTIVE", error.Code);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 51)]
    public async Task Paging_is_bounded(int page, int pageSize) =>
        await Assert.ThrowsAsync<ValidationException>(() => new Db().Sender().Send(new GetMyRefundsQuery(page, pageSize)));
}
