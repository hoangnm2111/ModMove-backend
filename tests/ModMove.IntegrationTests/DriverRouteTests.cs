using System.Data;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Drivers.Queries.GetDriverRoute;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.IntegrationTests;

/// <summary>The driver app asks the server for the road to the pickup point instead of calling MapVina itself.</summary>
public class DriverRouteTests
{
    private static TestApp SignedInDriver(string accountStatus = "Active")
    {
        var app = TestApp.SignedInAs(User.Create("Tai", "0900000003", "tai@x.vn", "h", UserRole.Driver, emailVerified: true));
        var account = new DataTable();
        account.Columns.Add("account_status", typeof(string));
        account.Rows.Add(accountStatus);
        app.Sql.Result = account;
        return app;
    }

    [Fact]
    public async Task Returns_distance_duration_and_polyline_of_the_route()
    {
        var app = SignedInDriver();
        app.Maps.Route = new RouteInfo(7.14m, 17, "abc");

        var route = await app.Sender.Send(new GetDriverRouteQuery(10.7769m, 106.7009m, 10.7609m, 106.6797m));

        Assert.Equal(new DriverRouteResponse(7.14m, 17, "abc"), route);
    }

    [Fact]
    public async Task Coordinates_outside_the_globe_are_rejected()
    {
        var app = SignedInDriver();

        await Assert.ThrowsAsync<ValidationException>(() =>
            app.Sender.Send(new GetDriverRouteQuery(91m, 106m, 10m, 106m)));
    }

    [Fact]
    public async Task Missing_coordinate_is_rejected_instead_of_routing_to_zero()
    {
        var app = SignedInDriver();

        await Assert.ThrowsAsync<ValidationException>(() =>
            app.Sender.Send(new GetDriverRouteQuery(10.7769m, 106.7009m, null, null)));
    }

    [Theory]
    [InlineData("Locked")]
    [InlineData("Inactive")]
    public async Task Locked_or_deleted_driver_is_refused_before_spending_a_MapVina_call_B8(string status)
    {
        var app = SignedInDriver(status);

        var forbidden = await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            app.Sender.Send(new GetDriverRouteQuery(10.7769m, 106.7009m, 10.7609m, 106.6797m)));

        Assert.Equal("ACCOUNT_INACTIVE", forbidden.Code);
    }
}
