using ModMove.Domain.Entities;

namespace ModMove.Application.Common.Interfaces.Persistence;

public interface IMasterDataRepository : IRepository<MasterDataItem>
{
    /// <summary>The entry a client may still choose, or null when the code is unknown or switched off.</summary>
    Task<MasterDataItem?> GetActiveAsync(string category, string code, CancellationToken cancellationToken = default);
}
