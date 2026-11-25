using Dapper;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Auth.Queries.CheckEmailAvailable;

internal sealed class CheckEmailAvailableQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<CheckEmailAvailableQuery, CheckEmailAvailableResponse>
{
    private const string Sql = "SELECT EXISTS(SELECT 1 FROM users WHERE email = @Email)";

    public async Task<CheckEmailAvailableResponse> Handle(CheckEmailAvailableQuery request, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var taken = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(Sql, new { Email = AuthRules.Normalize(request.Email) }, cancellationToken: cancellationToken));
        return new CheckEmailAvailableResponse(!taken);
    }
}
