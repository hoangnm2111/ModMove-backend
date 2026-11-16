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
        ORDER BY 
            CASE code 
                WHEN 'WARRANTY' THEN 1
                WHEN 'REFUND' THEN 2
                WHEN 'TERMS_OF_USE' THEN 3
                WHEN 'PRIVACY' THEN 4
                ELSE 99
            END,
            id
        """;

    public async Task<IReadOnlyList<ContentResponse>> Handle(GetPublicContentQuery request, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<ContentResponse>(
            new CommandDefinition(Sql, new { request.Category }, cancellationToken: cancellationToken));
        var list = rows.AsList();

        if (request.Category == PublicContentCategories.Policy)
        {
            return list.OrderBy(GetPolicySortOrder).ToList();
        }

        return list;
    }

    private static int GetPolicySortOrder(ContentResponse item) => item.Code switch
    {
        "WARRANTY" => 1,
        "REFUND" => 2,
        "TERMS_OF_USE" => 3,
        "PRIVACY" => 4,
        _ => 99
    };
}
