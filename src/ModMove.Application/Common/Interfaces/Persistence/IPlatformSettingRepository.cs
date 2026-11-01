using ModMove.Domain.Entities;

namespace ModMove.Application.Common.Interfaces.Persistence;

/// <summary>Write-side access to platform_settings rows (DECISIONS C8). Reads for pricing go through <see cref="IPlatformSettings"/>.</summary>
public interface IPlatformSettingRepository : IRepository<PlatformSetting>
{
    /// <summary>The tracked rows for these keys; a key without a row is simply absent from the result.</summary>
    Task<IReadOnlyList<PlatformSetting>> GetByKeysAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default);
}
