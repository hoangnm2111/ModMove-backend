using ModMove.Domain.Common;

namespace ModMove.Domain.Entities;

/// <summary>Lookup entry such as a cargo type or incident category (UC-ADM-14..16).</summary>
public sealed class MasterDataItem : BaseAuditableEntity, IAggregateRoot
{
    private MasterDataItem() { }

    public string Category { get; private set; } = null!;
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public bool IsActive { get; private set; } = true;

    public static MasterDataItem Create(string category, string code, string name) =>
        new() { Category = category, Code = code, Name = name };

    public void Rename(string name) => Name = name;

    public void SetActive(bool isActive) => IsActive = isActive;
}
