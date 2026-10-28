using System.Data;
using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;

namespace ModMove.Application.Features.Orders.Queries;

/// <summary>Read-side twin of LoadUserAsync: a deleted or locked account is refused even with a live token (B8).</summary>
internal static class CustomerAccess
{
    private const string Sql = "SELECT account_status AS AccountStatus FROM users WHERE id = @CustomerId";

    public static async Task EnsureActiveAsync(IDbConnection connection, long customerId, CancellationToken cancellationToken)
    {
        var status = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(Sql, new { CustomerId = customerId }, cancellationToken: cancellationToken))
            ?? throw new UnauthorizedException("Sign in again.");

        if (status != "Active")
            throw CurrentUserExtensions.InactiveAccount();
    }

    /// <summary>Database DATETIME comes back without an offset; the app stores UTC (DECISIONS 9.5).</summary>
    public static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
