using ModMove.Domain.Common;

namespace ModMove.Domain.Entities;

/// <summary>Key-value configuration managed by the Administrator (COMMISSION_RATE, LOADING_FEE...).</summary>
public sealed class PlatformSetting : BaseAuditableEntity, IAggregateRoot
{
    private PlatformSetting() { }

    public string SettingKey { get; private set; } = null!;
    public string SettingValue { get; private set; } = null!;

    /// <summary>UC-ADM-13: a new rate applies only to orders completed after this moment.</summary>
    public DateTimeOffset EffectiveFrom { get; private set; }

    public long? UpdatedBy { get; private set; }

    public static PlatformSetting Create(string key, string value, DateTimeOffset effectiveFrom) =>
        new() { SettingKey = key, SettingValue = value, EffectiveFrom = effectiveFrom };

    public void Change(string value, DateTimeOffset effectiveFrom, long adminId)
    {
        SettingValue = value;
        EffectiveFrom = effectiveFrom;
        UpdatedBy = adminId;
    }
}
