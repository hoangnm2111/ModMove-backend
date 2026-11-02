using System.Data;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ModMove.Application;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Features.Users.Queries.GetMyProfile;
using ModMove.Domain.Enums;

namespace ModMove.IntegrationTests;

/// <summary>GET api/account/profile through the real pipeline, with the users row served by <see cref="FakeSql"/>.</summary>
public class ProfileQueryTests
{
    private static ISender SignedIn(long userId, AccountStatus status) => SignedIn(userId, status, new FakeSql());

    private static ISender SignedIn(long userId, AccountStatus status, FakeSql sql)
    {
        var row = new DataTable();
        row.Columns.Add("Id", typeof(long));
        row.Columns.Add("FullName", typeof(string));
        row.Columns.Add("PhoneNumber", typeof(string));
        row.Columns.Add("Email", typeof(string));
        row.Columns.Add("Role", typeof(string));
        row.Columns.Add("EmailVerified", typeof(bool));
        row.Columns.Add("AccountStatus", typeof(string));
        row.Rows.Add(userId, "An", "0900000001", "an@x.vn", "Customer", true, status.ToString());
        sql.Result = row;

        return new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddSingleton<ISqlConnectionFactory>(sql)
            .AddSingleton<ICurrentUserService>(new FakeCurrentUser(userId))
            .BuildServiceProvider()
            .GetRequiredService<ISender>();
    }

    [Fact]
    public async Task Active_account_reads_its_profile()
    {
        var profile = await SignedIn(7, AccountStatus.Active).Send(new GetMyProfileQuery());

        Assert.Equal(7, profile.Id);
        Assert.Equal("an@x.vn", profile.Email);
        Assert.Equal("Customer", profile.Role);
        Assert.True(profile.EmailVerified);
    }

    // FakeSql does not run the SQL, so check it reads the real column and only the caller's own row (IDOR).
    [Fact]
    public async Task Query_reads_account_status_of_the_signed_in_user_only()
    {
        var sql = new FakeSql();

        await SignedIn(7, AccountStatus.Active, sql).Send(new GetMyProfileQuery());

        Assert.Contains("account_status AS AccountStatus", sql.LastSql);
        Assert.Contains("WHERE id = @Id", sql.LastSql);
        Assert.Equal(7L, sql.LastParameters["Id"]);
    }

    // A token outlives a deletion or an Admin lock by up to 15 minutes; the profile must not, like every other Account endpoint.
    [Theory]
    [InlineData(AccountStatus.Inactive)]
    [InlineData(AccountStatus.Locked)]
    public async Task Account_that_is_not_active_is_refused_B8(AccountStatus status)
    {
        var error = await Assert.ThrowsAsync<ForbiddenAccessException>(() => SignedIn(7, status).Send(new GetMyProfileQuery()));

        Assert.Equal("ACCOUNT_INACTIVE", error.Code);
    }
}
