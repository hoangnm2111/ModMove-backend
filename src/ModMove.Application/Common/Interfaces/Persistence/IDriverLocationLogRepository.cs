using ModMove.Domain.Entities;

namespace ModMove.Application.Common.Interfaces.Persistence;

public interface IDriverLocationLogRepository : IRepository<DriverLocationLog>
{
    /// <summary>JOB-GPS-01: deletes at once, without waiting for SaveChanges. Returns how many points went.</summary>
    Task<int> DeleteRecordedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default);

    /// <summary>B12: time of the order's latest logged point, or null when none is logged yet.</summary>
    Task<DateTimeOffset?> GetLastRecordedAtAsync(long orderId, CancellationToken cancellationToken = default);
}
