using Dapper;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.MasterData.Queries.GetMasterDataList;

internal sealed class GetMasterDataListQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetMasterDataListQuery, IReadOnlyList<MasterDataItemDto>>
{
    private const string SqlWithCategory = """
        SELECT id AS Id, category AS Category, code AS Code, name AS Name,
               description AS Description, is_active AS IsActive,
               created_at AS CreatedAt, updated_at AS UpdatedAt
        FROM master_data_items
        WHERE category = @Category
        ORDER BY id
        """;

    private const string SqlAll = """
        SELECT id AS Id, category AS Category, code AS Code, name AS Name,
               description AS Description, is_active AS IsActive,
               created_at AS CreatedAt, updated_at AS UpdatedAt
        FROM master_data_items
        ORDER BY category, id
        """;

    public async Task<IReadOnlyList<MasterDataItemDto>> Handle(GetMasterDataListQuery request, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = string.IsNullOrWhiteSpace(request.Category) ? SqlAll : SqlWithCategory;
        var rows = await connection.QueryAsync<MasterDataItemDto>(
            new CommandDefinition(sql, new { request.Category }, cancellationToken: cancellationToken));
        return rows.AsList();
    }
}
