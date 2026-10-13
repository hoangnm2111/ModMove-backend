using System.Data;

namespace ModMove.Application.Common.Interfaces.Persistence;

/// <summary>
/// Read-side connection for query handlers (use with Dapper).
/// InnoDB plain SELECTs are consistent non-locking reads, so queries never block commands.
/// </summary>
public interface ISqlConnectionFactory
{
    IDbConnection CreateConnection();
}
