using System.Data;
using ModMove.Application.Common.Interfaces.Persistence;
using MySqlConnector;

namespace ModMove.Infrastructure.Persistence;

internal sealed class SqlConnectionFactory(string connectionString) : ISqlConnectionFactory
{
    public IDbConnection CreateConnection() => new MySqlConnection(connectionString);
}
