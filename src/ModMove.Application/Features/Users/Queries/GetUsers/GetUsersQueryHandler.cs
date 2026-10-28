using Dapper;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;

namespace ModMove.Application.Features.Users.Queries.GetUsers;

internal sealed class GetUsersQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetUsersQuery, PagedResult<UserDto>>
{
    private const string CountSql = """
        SELECT COUNT(1)
        FROM users
        WHERE (@Role IS NULL OR role = @Role)
          AND (@Status IS NULL OR account_status = @Status)
          AND (@SearchPattern IS NULL
               OR full_name LIKE @SearchPattern
               OR phone_number LIKE @SearchPattern
               OR email LIKE @SearchPattern)
        """;

    private const string ItemsSql = """
        SELECT id AS Id, full_name AS FullName, phone_number AS PhoneNumber, email AS Email,
               role AS Role, account_status AS AccountStatus, email_verified AS EmailVerified,
               created_at AS CreatedAt
        FROM users
        WHERE (@Role IS NULL OR role = @Role)
          AND (@Status IS NULL OR account_status = @Status)
          AND (@SearchPattern IS NULL
               OR full_name LIKE @SearchPattern
               OR phone_number LIKE @SearchPattern
               OR email LIKE @SearchPattern)
        ORDER BY id DESC
        LIMIT @PageSize OFFSET @Offset
        """;

    public async Task<PagedResult<UserDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var offset = (page - 1) * pageSize;

        var role = request.Role?.ToString();
        var status = request.Status?.ToString();
        var searchPattern = string.IsNullOrWhiteSpace(request.Search)
            ? null
            : $"%{request.Search.Trim()}%";

        var parameters = new
        {
            Role = role,
            Status = status,
            SearchPattern = searchPattern,
            PageSize = pageSize,
            Offset = offset
        };

        using var connection = connectionFactory.CreateConnection();
        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(CountSql, parameters, cancellationToken: cancellationToken));

        var items = await connection.QueryAsync<UserDto>(
            new CommandDefinition(ItemsSql, parameters, cancellationToken: cancellationToken));

        return new PagedResult<UserDto>(items.AsList(), page, pageSize, totalCount);
    }
}

