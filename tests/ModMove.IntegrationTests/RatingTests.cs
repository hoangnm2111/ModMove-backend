using System.Data;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ModMove.Application;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Features.Ratings.Commands.RateTrip;
using ModMove.Application.Features.Ratings.Queries.GetMyRatings;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

/// <summary>POST api/customer/ratings (UC-CUS-25; GB-20, DECISIONS 7.5, C10).</summary>
public class RateTripTests
{
    private static ShippingOrder Completed(TripSetup s, long? customerId = null)
    {
        var order = s.Delivered(customerId);
        order.Complete(0.10m, ChangedByRole.System, null, TripSetup.Now);
        return order;
    }

    [Fact]
    public async Task Customer_rates_the_driver_of_a_completed_trip_once_GB20()
    {
        var s = new TripSetup();
        var order = Completed(s);

        var result = await s.App.Sender.Send(new RateTripCommand(order.Id, 4, " Đúng giờ "));

        var rating = Assert.Single(s.App.Ratings.Items);
        Assert.Equal(rating.Id, result.RatingId);
        Assert.Equal((order.Id, s.Customer.Id, s.Profile.Id, 4), (rating.OrderId, rating.CustomerId, rating.DriverProfileId, rating.RatingStars));
        Assert.Equal("Đúng giờ", rating.ReviewComment);
        Assert.Equal(1, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task The_driver_is_told_about_a_new_rating_B17()
    {
        var s = new TripSetup();
        var order = Completed(s);
        s.App.Realtime.Sent.Clear();

        var result = await s.App.Sender.Send(new RateTripCommand(order.Id, 2, "Đến trễ"));

        var notification = s.SentToDriver();
        Assert.Equal("RATING_RECEIVED", notification.Type);
        Assert.Contains("2 sao", notification.Body);
        Assert.Equal((order.Id.ToString(), result.RatingId.ToString()), (notification.Data!["orderId"], notification.Data["ratingId"]));
    }

    [Fact]
    public async Task A_refused_rating_tells_the_driver_nothing()
    {
        var s = new TripSetup();
        var order = Completed(s);
        await s.App.Sender.Send(new RateTripCommand(order.Id, 4, null));
        s.App.Realtime.Sent.Clear();

        await Assert.ThrowsAsync<ConflictException>(() => s.App.Sender.Send(new RateTripCommand(order.Id, 1, null)));

        Assert.Empty(s.App.Realtime.Sent);
    }

    [Fact]
    public async Task Empty_comment_is_stored_as_none()
    {
        var s = new TripSetup();
        var order = Completed(s);

        await s.App.Sender.Send(new RateTripCommand(order.Id, 5, "  "));

        Assert.Null(s.App.Ratings.Items.Single().ReviewComment);
    }

    [Fact]
    public async Task A_rating_cannot_be_sent_again_or_edited_GB20()
    {
        var s = new TripSetup();
        var order = Completed(s);
        await s.App.Sender.Send(new RateTripCommand(order.Id, 4, null));

        await Assert.ThrowsAsync<ConflictException>(() => s.App.Sender.Send(new RateTripCommand(order.Id, 1, "đổi ý")));

        Assert.Equal(4, s.App.Ratings.Items.Single().RatingStars);
        Assert.Equal(1, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Trip_that_is_not_completed_cannot_be_rated()
    {
        var s = new TripSetup();
        var order = s.Delivered();

        var error = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new RateTripCommand(order.Id, 5, null)));

        Assert.Equal("RATING_NOT_ALLOWED", error.Code);
        Assert.Empty(s.App.Ratings.Items);
    }

    [Fact]
    public async Task Rating_closes_one_day_after_completion_7_5()
    {
        var s = new TripSetup();
        var order = Completed(s);
        s.App.Time.Now = TripSetup.Now.AddDays(1).AddMinutes(1);

        var error = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new RateTripCommand(order.Id, 5, null)));

        Assert.Equal("RATING_WINDOW_CLOSED", error.Code);
    }

    [Fact]
    public async Task Another_customers_trip_cannot_be_rated_C10()
    {
        var s = new TripSetup();
        var order = Completed(s, customerId: s.Other.Id);

        var error = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new RateTripCommand(order.Id, 1, null)));

        Assert.Equal("ORDER_NOT_YOURS", error.Code);
        Assert.Empty(s.App.Ratings.Items);
    }

    [Fact]
    public async Task Exactly_one_day_after_completion_is_still_in_time_7_5()
    {
        var s = new TripSetup();
        var order = Completed(s);
        s.App.Time.Now = TripSetup.Now.AddDays(1);

        await s.App.Sender.Send(new RateTripCommand(order.Id, 5, null));

        Assert.Single(s.App.Ratings.Items);
    }

    [Fact]
    public async Task Another_customer_learns_nothing_about_an_already_rated_trip_C10()
    {
        var s = new TripSetup();
        var order = Completed(s, customerId: s.Other.Id);
        s.App.Ratings.Add(order.Rate(s.Other.Id, 5, null, TripSetup.Now));

        var error = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new RateTripCommand(order.Id, 1, null)));

        Assert.Equal("ORDER_NOT_YOURS", error.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Stars_are_1_to_5(int stars)
    {
        var s = new TripSetup();
        var order = Completed(s);

        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new RateTripCommand(order.Id, stars, null)));
    }

    [Fact]
    public async Task Comment_is_at_most_1000_characters()
    {
        var s = new TripSetup();
        var order = Completed(s);

        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new RateTripCommand(order.Id, 5, new string('x', 1001))));
    }

    [Fact]
    public async Task Unknown_order_is_not_found() =>
        await Assert.ThrowsAsync<NotFoundException>(() => new TripSetup().App.Sender.Send(new RateTripCommand(999, 5, null)));

    [Fact]
    public async Task Locked_account_is_refused_B8()
    {
        var s = new TripSetup(AccountStatus.Locked);
        var order = Completed(s);

        var error = await Assert.ThrowsAsync<ForbiddenAccessException>(() => s.App.Sender.Send(new RateTripCommand(order.Id, 5, null)));

        Assert.Equal("ACCOUNT_INACTIVE", error.Code);
    }

    [Fact]
    public async Task Signed_out_caller_is_unauthorized() =>
        await Assert.ThrowsAsync<UnauthorizedException>(() => new TestApp().Sender.Send(new RateTripCommand(1, 5, null)));
}

/// <summary>GET api/customer/ratings (UC-CUS-26); rows served by FakeSql.</summary>
public class MyRatingsQueryTests
{
    private const long CustomerId = 7;

    private sealed class Db
    {
        public FakeSql Sql { get; } = new();
        public string AccountStatus { get; set; } = "Active";
        public List<(string Sql, IReadOnlyDictionary<string, object?> Parameters)> Executed { get; } = [];

        public Db()
        {
            Sql.ScalarResult = 1;
            Sql.CustomQuery = (text, parameters) =>
            {
                Executed.Add((text, parameters));
                if (text.Contains("FROM users"))
                    return Table(("AccountStatus", typeof(string), AccountStatus));
                return Table(
                    ("Id", typeof(long), 3L), ("OrderId", typeof(long), 1L), ("OrderCode", typeof(string), "MM20261020001"),
                    ("RatingStars", typeof(int), 5), ("ReviewComment", typeof(string), "Tốt"), ("DriverName", typeof(string), "Tài"),
                    ("CreatedAt", typeof(DateTime), new DateTime(2026, 10, 20, 9, 0, 0)));
            };
        }

        public ISender Sender() => new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddSingleton<ISqlConnectionFactory>(Sql)
            .AddSingleton<ICurrentUserService>(new FakeCurrentUser(CustomerId))
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

    [Fact]
    public async Task Lists_only_the_callers_ratings_newest_first()
    {
        var db = new Db();

        var page = await db.Sender().Send(new GetMyRatingsQuery(Page: 2, PageSize: 10));

        var (sql, parameters) = db.Executed.Last(e => e.Sql.Contains("FROM ratings"));
        Assert.Contains("r.customer_id = @CustomerId", sql);
        Assert.Contains("ORDER BY r.created_at DESC", sql);
        Assert.Equal(CustomerId, parameters["CustomerId"]);
        Assert.Equal(10, parameters["Offset"]);
        var item = Assert.Single(page.Items);
        Assert.Equal((3L, "MM20261020001", 5, "Tài"), (item.Id, item.OrderCode, item.RatingStars, item.DriverName));
        Assert.Equal(new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero), item.CreatedAt);
        Assert.Equal((2, 10, 1), (page.PageNumber, page.PageSize, page.TotalCount));
    }

    [Fact]
    public async Task Locked_account_is_refused_B8()
    {
        var db = new Db { AccountStatus = "Locked" };

        var error = await Assert.ThrowsAsync<ForbiddenAccessException>(() => db.Sender().Send(new GetMyRatingsQuery()));

        Assert.Equal("ACCOUNT_INACTIVE", error.Code);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(10_001, 20)]
    [InlineData(1, 51)]
    public async Task Paging_is_bounded(int page, int pageSize) =>
        await Assert.ThrowsAsync<ValidationException>(() => new Db().Sender().Send(new GetMyRatingsQuery(page, pageSize)));
}
