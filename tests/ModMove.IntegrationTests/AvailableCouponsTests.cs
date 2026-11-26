using System.Data;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ModMove.Application;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Features.Orders.Queries.GetAvailableCoupons;

namespace ModMove.IntegrationTests;

/// <summary>GET api/customer/coupons (UC-GST-05, GB-21); rows served by FakeSql.</summary>
public class AvailableCouponsTests
{
    private const long CustomerId = 7;

    private sealed class Db
    {
        public FakeSql Sql { get; } = new();
        public string AccountStatus { get; set; } = "Active";
        public List<(string Sql, IReadOnlyDictionary<string, object?> Parameters)> Executed { get; } = [];

        public Db()
        {
            Sql.ScalarResult = 2;
            Sql.CustomQuery = (text, parameters) =>
            {
                Executed.Add((text, parameters));
                if (text.Contains("FROM users"))
                {
                    var account = new DataTable();
                    account.Columns.Add("AccountStatus", typeof(string));
                    account.Rows.Add(AccountStatus);
                    return account;
                }

                var rows = new DataTable();
                rows.Columns.Add("DiscountCode", typeof(string));
                rows.Columns.Add("DiscountValue", typeof(decimal));
                rows.Columns.Add("MinimumOrderValue", typeof(decimal));
                rows.Columns.Add("ExpiresAt", typeof(DateTime));
                rows.Columns.Add("RemainingUses", typeof(int));
                rows.Rows.Add("WELCOME20K", 20_000m, 100_000m, new DateTime(2026, 12, 31, 16, 0, 0), 12);
                rows.Rows.Add("FREESHIP", 10_000m, 0m, new DateTime(2027, 1, 31, 16, 0, 0), DBNull.Value);
                return rows;
            };
        }

        public ISender Sender(long? userId = CustomerId) => new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddSingleton<ISqlConnectionFactory>(Sql)
            .AddSingleton<ICurrentUserService>(new FakeCurrentUser(userId))
            .AddSingleton<TimeProvider>(new MutableTime(TestApp.Start))
            .BuildServiceProvider()
            .GetRequiredService<ISender>();
    }

    [Fact]
    public async Task Lists_coupons_with_what_is_left_to_use()
    {
        var page = await new Db().Sender().Send(new GetAvailableCouponsQuery());

        Assert.Equal(2, page.TotalCount);
        var limited = page.Items[0];
        Assert.Equal(("WELCOME20K", 20_000m, 100_000m, (int?)12), (limited.DiscountCode, limited.DiscountValue, limited.MinimumOrderValue, limited.RemainingUses));
        Assert.Equal(new DateTimeOffset(2026, 12, 31, 16, 0, 0, TimeSpan.Zero), limited.ExpiresAt);
        Assert.Null(page.Items[1].RemainingUses); // no usage limit
    }

    [Fact]
    public async Task Only_coupons_a_new_order_could_use_are_read_GB21()
    {
        var db = new Db();

        await db.Sender().Send(new GetAvailableCouponsQuery());

        var (sql, parameters) = db.Executed.Last(e => e.Sql.Contains("FROM coupons"));
        Assert.Contains("is_active = 1", sql);
        Assert.Contains("valid_from <= @Now", sql);
        Assert.Contains("expires_at >= @Now", sql);
        Assert.Contains("usage_limit IS NULL OR used_count < usage_limit", sql);
        Assert.Equal(TestApp.Start.UtcDateTime, parameters["Now"]);
    }

    [Fact]
    public async Task Locked_account_is_refused_B8()
    {
        var error = await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new Db { AccountStatus = "Locked" }.Sender().Send(new GetAvailableCouponsQuery()));

        Assert.Equal("ACCOUNT_INACTIVE", error.Code);
    }

    [Fact]
    public async Task Signed_out_caller_is_unauthorized() =>
        await Assert.ThrowsAsync<UnauthorizedException>(() => new Db().Sender(userId: null).Send(new GetAvailableCouponsQuery()));

    [Theory]
    [InlineData(0, 20)]
    [InlineData(10_001, 20)]
    [InlineData(1, 51)]
    public async Task Paging_is_bounded(int page, int pageSize) =>
        await Assert.ThrowsAsync<ValidationException>(() => new Db().Sender().Send(new GetAvailableCouponsQuery(page, pageSize)));
}
