using Dapper;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.MasterData.Queries.GetPublicContent;

internal sealed class GetPublicContentQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetPublicContentQuery, IReadOnlyList<ContentResponse>>
{
    private const string Sql = """
        SELECT code AS Code, name AS Name, description AS Description
        FROM master_data_items
        WHERE category = @Category AND is_active = 1
        ORDER BY id
        """;

    public async Task<IReadOnlyList<ContentResponse>> Handle(GetPublicContentQuery request, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<ContentResponse>(
            new CommandDefinition(Sql, new { request.Category }, cancellationToken: cancellationToken));
        return rows.AsList();
    }
}
