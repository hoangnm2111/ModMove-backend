using System.Data;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ModMove.Application;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Features.Refunds;
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
        public string? DecisionReason { get; set; }
        public string? PayoutAccountNumber { get; set; }
        public string? PayoutFailureReason { get; set; }
        public string? PayerBankCode { get; set; } = "970436";
        public string? PayerBankName { get; set; } = "Vietcombank";
        public string? PayerAccountNumber { get; set; }
        public string? PayerAccountName { get; set; } = "NGUYEN VAN A";
        public List<(string Sql, IReadOnlyDictionary<string, object?> Parameters)> Executed { get; } = [];

        public Db()
        {
            Sql.ScalarResult = 1;
            Sql.CustomQuery = (text, parameters) =>
            {
                Executed.Add((text, parameters));
                if (text.Contains("FROM users"))
                    return Table(("AccountStatus", typeof(string), AccountStatus));
                if (text.Contains("FROM payment_transactions"))
                {
                    var payer = Table(
                        ("BankCode", typeof(string), PayerBankCode),
                        ("BankName", typeof(string), PayerBankName),
                        ("AccountNumber", typeof(string), PayerAccountNumber),
                        ("AccountHolder", typeof(string), PayerAccountName));
                    if (PayerAccountNumber is null)
                        payer.Rows.Clear();
                    return payer;
                }
                var row = Table(
                    ("Id", typeof(long), 4L), ("OrderId", typeof(long), 1L), ("OrderCode", typeof(string), "MM20261020001"),
                    ("IncidentId", typeof(long), null), ("SourceType", typeof(string), "Complaint"), ("RefundStatus", typeof(string), Status),
                    ("Reason", typeof(string), "Hàng vỡ"), ("ReasonCode", typeof(string), "DAMAGED_CARGO"),
                    ("DecisionReason", typeof(string), DecisionReason),
                    ("PayoutBankCode", typeof(string), PayoutAccountNumber is null ? null : "970436"),
                    ("PayoutBankName", typeof(string), PayoutAccountNumber is null ? null : "Vietcombank"),
                    ("PayoutAccountNumber", typeof(string), PayoutAccountNumber),
                    ("PayoutAccountHolder", typeof(string), PayoutAccountNumber is null ? null : "NGUYEN VAN A"),
                    ("PayoutFailureReason", typeof(string), PayoutFailureReason),
                    ("BankReferenceCode", typeof(string), "FT26293998877"),
                    ("EvidenceUrls", typeof(string), """["https://res.cloudinary.com/demo/a.jpg"]"""),
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
    public async Task Customer_sees_the_admins_reason_and_the_reason_code_CUS23()
    {
        var db = new Db { Status = "Rejected", DecisionReason = "Ảnh không cho thấy hư hỏng" };

        var detail = await db.Sender().Send(new GetMyRefundDetailQuery(4));
        var page = await db.Sender().Send(new GetMyRefundsQuery());

        Assert.Equal(("DAMAGED_CARGO", "Ảnh không cho thấy hư hỏng"), (detail.ReasonCode, detail.DecisionReason));
        Assert.Equal("Ảnh không cho thấy hư hỏng", Assert.Single(page.Items).DecisionReason);
    }

    [Fact]
    public async Task The_dispatchers_proposal_reason_is_never_read_for_the_customer()
    {
        var db = new Db();

        await db.Sender().Send(new GetMyRefundDetailQuery(4));

        Assert.DoesNotContain("proposal_reason", db.RefundsQuery().Sql);
    }

    // ---- Payout account (R-01) ----

    [Fact]
    public async Task The_customer_sees_the_payout_account_masked_and_never_in_full_R01()
    {
        var db = new Db { Status = "Approved", PayoutAccountNumber = "0123456789" };

        var detail = await db.Sender().Send(new GetMyRefundDetailQuery(4));
        var page = await db.Sender().Send(new GetMyRefundsQuery());

        Assert.Equal(new PayoutAccountView("970436", "Vietcombank", "•••• 6789", "NGUYEN VAN A"), detail.PayoutAccount);
        Assert.False(detail.NeedsPayoutAccount);
        Assert.False(Assert.Single(page.Items).NeedsPayoutAccount);
        var json = System.Text.Json.JsonSerializer.Serialize(new object[] { detail, page });
        Assert.DoesNotContain("0123456789", json);
        Assert.DoesNotContain("012345", json);
    }

    [Theory]
    [InlineData("Requested", null, true)]
    [InlineData("Approved", null, true)]
    [InlineData("Failed", null, true)]
    [InlineData("Processed", null, false)]
    [InlineData("Rejected", null, false)]
    [InlineData("Withdrawn", null, false)]
    [InlineData("Requested", "0123456789", false)]
    [InlineData("Failed", "0123456789", false)]
    public async Task NeedsPayoutAccount_follows_the_status_and_whether_one_was_given_R01(string status, string? account, bool expected)
    {
        var db = new Db { Status = status, PayoutAccountNumber = account };

        var detail = await db.Sender().Send(new GetMyRefundDetailQuery(4));
        var page = await db.Sender().Send(new GetMyRefundsQuery());

        Assert.Equal(expected, detail.NeedsPayoutAccount);
        Assert.Equal(expected, Assert.Single(page.Items).NeedsPayoutAccount);
        Assert.Equal(account is null, detail.PayoutAccount is null);
    }

    [Theory]
    [InlineData("Processed", "FT26293998877")]
    [InlineData("Approved", null)]
    [InlineData("Failed", null)]
    public async Task The_bank_reference_shows_only_once_the_refund_is_paid_R01(string status, string? expected)
    {
        var detail = await new Db { Status = status, PayoutAccountNumber = "0123456789" }.Sender().Send(new GetMyRefundDetailQuery(4));

        Assert.Equal(expected, detail.BankReferenceCode);
    }

    [Fact]
    public async Task A_failed_transfer_tells_the_customer_why_R01()
    {
        var detail = await new Db { Status = "Failed", PayoutFailureReason = "Sai số tài khoản" }.Sender().Send(new GetMyRefundDetailQuery(4));

        Assert.Equal("Sai số tài khoản", detail.PayoutFailureReason);
        Assert.True(detail.NeedsPayoutAccount);
    }

    [Fact]
    public async Task PayOS_suggestion_is_returned_masked_and_normalized_when_refund_needs_an_account_R01()
    {
        var db = new Db
        {
            Status = "Requested",
            PayerAccountNumber = "0011001234567",
            PayerAccountName = "Nguyễn   Văn Á",
        };

        var detail = await db.Sender().Send(new GetMyRefundDetailQuery(4));

        Assert.Equal(new PayoutAccountView("970436", "Vietcombank", "•••• 4567", "NGUYEN VAN A"), detail.SuggestedPayoutAccount);
        var suggestionQuery = Assert.Single(db.Executed, e => e.Sql.Contains("FROM payment_transactions"));
        Assert.Equal(1L, suggestionQuery.Parameters["OrderId"]);
        Assert.Contains("b.category = 'BANK'", suggestionQuery.Sql);
        Assert.Contains("b.is_active = 1", suggestionQuery.Sql);
        Assert.Contains("p.transaction_status = 'Paid'", suggestionQuery.Sql);
        var json = System.Text.Json.JsonSerializer.Serialize(detail);
        Assert.DoesNotContain("0011001234567", json);
        Assert.DoesNotContain("001100", json);
    }

    [Theory]
    [InlineData("Requested", "0123456789")]
    [InlineData("Processed", null)]
    [InlineData("Rejected", null)]
    [InlineData("Withdrawn", null)]
    public async Task PayOS_suggestion_is_omitted_and_not_queried_once_account_is_set_or_closed_R01(string status, string? account)
    {
        var db = new Db
        {
            Status = status,
            PayoutAccountNumber = account,
            PayerAccountNumber = "0011001234567",
        };

        var detail = await db.Sender().Send(new GetMyRefundDetailQuery(4));

        Assert.Null(detail.SuggestedPayoutAccount);
        Assert.DoesNotContain(db.Executed, e => e.Sql.Contains("FROM payment_transactions"));
    }

    [Theory]
    [InlineData("12345", "NGUYEN VAN A")]
    [InlineData("123456abc", "NGUYEN VAN A")]
    [InlineData("0123456789", "1")]
    [InlineData("0123456789", null)]
    public async Task PayOS_suggestion_ignores_unusable_payer_numbers_or_names_R01(string number, string? name)
    {
        var db = new Db
        {
            Status = "Requested",
            PayerAccountNumber = number,
            PayerAccountName = name,
        };

        var detail = await db.Sender().Send(new GetMyRefundDetailQuery(4));

        Assert.Null(detail.SuggestedPayoutAccount);
    }

    [Theory]
    [InlineData("0123456789", "•••• 6789")]
    [InlineData("123456", "•••• 3456")]
    [InlineData("1234567890123456789", "•••• 6789")]
    public void Masking_keeps_the_last_four_digits_R01(string number, string expected) =>
        Assert.Equal(expected, PayoutAccountView.Mask(number));

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
