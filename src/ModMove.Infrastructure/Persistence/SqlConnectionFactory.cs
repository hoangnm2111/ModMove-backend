using System.Data;
using Dapper;
using ModMove.Application.Common.Interfaces.Persistence;
using MySqlConnector;

namespace ModMove.Infrastructure.Persistence;

internal sealed class SqlConnectionFactory(string connectionString) : ISqlConnectionFactory
{
    static SqlConnectionFactory()
    {
        SqlMapper.AddTypeHandler(new UtcDateTimeOffsetTypeHandler());
        SqlMapper.AddTypeHandler(new NullableUtcDateTimeOffsetTypeHandler());
    }

    public static void EnsureTypeHandlersRegistered()
    {
        // Calling this triggers the static constructor once.
    }

    public IDbConnection CreateConnection() => new MySqlConnection(connectionString);
}
