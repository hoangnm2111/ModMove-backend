using ModMove.Domain.Entities;

namespace ModMove.Application.Common.Interfaces.Persistence;

public interface IDriverLocationLogRepository : IRepository<DriverLocationLog>
{
    /// <summary>JOB-GPS-01: deletes at once, without waiting for SaveChanges. Returns how many points went.</summary>
    Task<int> DeleteRecordedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default);
}
