using System.Data;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ModMove.Application;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Features.Incidents.Queries.GetIncidentById;
using ModMove.Application.Features.Refunds.Queries.GetAdminRefundDetail;
using ModMove.Application.Features.Refunds.Queries.GetAdminRefunds;
using ModMove.Application.Features.Refunds.Queries.GetComplaintById;

namespace ModMove.IntegrationTests;

/// <summary>GET api/admin/refunds and refunds/{id} (K4, R-01): full payout account and hasPayoutAccount for Admin only.</summary>
public class AdminRefundsQueryTests
{
    private sealed class Db
    {
        public FakeSql Sql { get; } = new();
        public bool Found { get; set; } = true;
        public string Status { get; set; } = "Approved";
        public string? PayoutAccountNumber { get; set; }
        public string? PayoutFailureReason { get; set; }
        public DateTime? PayoutInfoUpdatedAt { get; set; }
        public string? EvidenceUrlsJson { get; set; }
        public List<(string Sql, IReadOnlyDictionary<string, object?> Parameters)> Executed { get; } = [];

        public Db()
        {
            Sql.ScalarResult = 1;
            Sql.CustomQuery = (text, parameters) =>
            {
                Executed.Add((text, parameters));
                if (text.Contains("FROM order_status_histories") || text.Contains("FROM in_transit_incidents i"))
                    return new DataTable();

                if (text.Contains("FROM refunds r") && text.Contains("JOIN shipping_orders o"))
                {
                    var row = text.Contains("r.payout_info_updated_at")
                        ? DetailRow()
                        : ListRow();
                    if (!Found)
                        row.Rows.Clear();
                    return row;
                }

                return new DataTable();
            };
        }

        private DataTable ListRow() => Table(
            ("Id", typeof(long), 4L),
            ("OrderId", typeof(long), 1L),
            ("OrderCode", typeof(string), "MM20261020001"),
            ("CustomerName", typeof(string), "An"),
            ("CustomerPhone", typeof(string), "0900000001"),
            ("SourceType", typeof(string), "Cancellation"),
            ("RefundStatus", typeof(string), Status),
            ("RequestedAmount", typeof(decimal), 180_000m),
            ("ProposedAmount", typeof(decimal), 180_000m),
            ("ReasonCode", typeof(string), "CHANGED_PLAN"),
            ("Reason", typeof(string), "Đổi ý"),
            ("ProposalReason", typeof(string), null),
            ("ProposedByName", typeof(string), null),
            ("DecisionNote", typeof(string), null),
            ("BankReferenceCode", typeof(string), null),
            ("DecidedAt", typeof(DateTime), new DateTime(2026, 10, 20, 10, 0, 0)),
            ("ProcessedAt", typeof(DateTime), null),
            ("CreatedAt", typeof(DateTime), new DateTime(2026, 10, 20, 9, 0, 0)),
            ("PayoutAccountNumber", typeof(string), PayoutAccountNumber),
            ("PayoutFailureReason", typeof(string), PayoutFailureReason));

        private DataTable DetailRow() => Table(
            ("Id", typeof(long), 4L),
            ("OrderId", typeof(long), 1L),
            ("OrderCode", typeof(string), "MM20261020001"),
            ("OrderStatus", typeof(string), "Cancelled"),
            ("OrderTotalFee", typeof(decimal), 180_000m),
            ("PaymentMethod", typeof(string), "PayOS"),
            ("PaymentStatus", typeof(string), "Paid"),
            ("IncidentId", typeof(long), null),
            ("SourceType", typeof(string), "Cancellation"),
            ("RefundStatus", typeof(string), Status),
            ("ReasonCode", typeof(string), "CHANGED_PLAN"),
            ("Reason", typeof(string), "Đổi ý"),
            ("EvidenceUrlsJson", typeof(string), EvidenceUrlsJson),
            ("RequestedAmount", typeof(decimal), 180_000m),
            ("ProposedAmount", typeof(decimal), 180_000m),
            ("ProposalReason", typeof(string), null),
            ("ProposedById", typeof(long), null),
            ("ProposedByName", typeof(string), null),
            ("ApprovedById", typeof(long), 99L),
            ("ApprovedByName", typeof(string), "Admin"),
            ("DecisionNote", typeof(string), null),
            ("InfoRequestNote", typeof(string), null),
            ("CustomerReply", typeof(string), null),
            ("BankReferenceCode", typeof(string), null),
            ("DecidedAt", typeof(DateTime), new DateTime(2026, 10, 20, 10, 0, 0)),
            ("ProcessedAt", typeof(DateTime), null),
            ("CreatedAt", typeof(DateTime), new DateTime(2026, 10, 20, 9, 0, 0)),
            ("PayoutBankCode", typeof(string), PayoutAccountNumber is null ? null : "970436"),
            ("PayoutBankName", typeof(string), PayoutAccountNumber is null ? null : "Vietcombank"),
            ("PayoutAccountNumber", typeof(string), PayoutAccountNumber),
            ("PayoutAccountHolder", typeof(string), PayoutAccountNumber is null ? null : "NGUYEN VAN A"),
            ("PayoutInfoUpdatedAt", typeof(DateTime), PayoutInfoUpdatedAt),
            ("PayoutFailureReason", typeof(string), PayoutFailureReason),
            ("CustomerId", typeof(long), 7L),
            ("CustomerName", typeof(string), "An"),
            ("CustomerPhone", typeof(string), "0900000001"),
            ("CustomerEmail", typeof(string), "an@x.vn"),
            ("DriverProfileId", typeof(long), null),
            ("DriverName", typeof(string), null),
            ("DriverPhone", typeof(string), null),
            ("LicensePlate", typeof(string), null));

        public ISender Sender() => new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddSingleton<ISqlConnectionFactory>(Sql)
            .AddSingleton<IFileStorage>(new FakeFileStorage())
            .BuildServiceProvider()
            .GetRequiredService<ISender>();
    }

    private static DataTable Table(params (string Name, Type Type, object? Value)[] columns)
    {
        var table = new DataTable();
        foreach (var c in columns)
            table.Columns.Add(c.Name, c.Type);
        table.Rows.Add(columns.Select(c => c.Value ?? DBNull.Value).ToArray());
        return table;
    }

    [Theory]
    [InlineData(null, false, null)]
    [InlineData("0123456789", true, "Sai tên chủ tài khoản")]
    public async Task List_exposes_hasPayoutAccount_and_failure_reason_without_exposing_account_numbers_R01(
        string? accountNumber, bool expectedHasAccount, string? failureReason)
    {
        var db = new Db { PayoutAccountNumber = accountNumber, PayoutFailureReason = failureReason };

        var page = await db.Sender().Send(new GetAdminRefundsQuery());

        var item = Assert.Single(page.Items);
        Assert.Equal(expectedHasAccount, item.HasPayoutAccount);
        Assert.Equal(failureReason, item.PayoutFailureReason);
        var json = System.Text.Json.JsonSerializer.Serialize(page);
        Assert.DoesNotContain("0123456789", json);
    }

    [Fact]
    public async Task GetAdminRefundDetail_signs_private_photos_DEF011()
    {
        var db = new Db { EvidenceUrlsJson = FakeFileStorage.MixedPhotosJson };

        var detail = await db.Sender().Send(new GetAdminRefundDetailQuery(4));

        Assert.Equal(FakeFileStorage.MixedPhotosViewed, detail.EvidenceUrls);
    }

    [Fact]
    public async Task Detail_returns_full_unmasked_payout_account_for_Admin_manual_transfer_R01()
    {
        var updatedAt = new DateTime(2026, 10, 20, 11, 30, 0);
        var db = new Db
        {
            Status = "Failed",
            PayoutAccountNumber = "0123456789",
            PayoutInfoUpdatedAt = updatedAt,
            PayoutFailureReason = "Ngân hàng bảo trì",
        };

        var detail = await db.Sender().Send(new GetAdminRefundDetailQuery(4));

        Assert.True(detail.HasPayoutAccount);
        Assert.Equal("Ngân hàng bảo trì", detail.PayoutFailureReason);
        Assert.NotNull(detail.PayoutAccount);
        Assert.Equal(("970436", "Vietcombank", "0123456789", "NGUYEN VAN A"),
            (detail.PayoutAccount!.BankCode, detail.PayoutAccount.BankName, detail.PayoutAccount.AccountNumber, detail.PayoutAccount.AccountHolder));
        Assert.Equal(new DateTimeOffset(updatedAt, TimeSpan.Zero), detail.PayoutAccount.InfoUpdatedAt);
    }

    [Fact]
    public async Task Detail_returns_null_payout_account_and_false_flag_when_customer_has_not_given_one_R01()
    {
        var db = new Db { PayoutAccountNumber = null };

        var detail = await db.Sender().Send(new GetAdminRefundDetailQuery(4));

        Assert.False(detail.HasPayoutAccount);
        Assert.Null(detail.PayoutAccount);
    }

    [Fact]
    public async Task Detail_throws_NotFoundException_when_refund_does_not_exist() =>
        await Assert.ThrowsAsync<NotFoundException>(() => new Db { Found = false }.Sender().Send(new GetAdminRefundDetailQuery(999)));

    [Fact]
    public async Task Dispatcher_complaint_and_incident_queries_never_select_payout_columns_Q7()
    {
        var sql = new FakeSql();
        var recorded = new List<string>();
        sql.CustomQuery = (text, _) =>
        {
            recorded.Add(text);
            return new DataTable();
        };
        var sender = new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddSingleton<ISqlConnectionFactory>(sql)
            .AddSingleton<IFileStorage>(new FakeFileStorage())
            .BuildServiceProvider()
            .GetRequiredService<ISender>();

        await Assert.ThrowsAsync<NotFoundException>(() => sender.Send(new GetComplaintByIdQuery(1)));
        await Assert.ThrowsAsync<NotFoundException>(() => sender.Send(new GetIncidentByIdQuery(1)));

        Assert.Equal(2, recorded.Count);
        Assert.All(recorded, s => Assert.DoesNotContain("payout_", s, StringComparison.OrdinalIgnoreCase));
    }
}
