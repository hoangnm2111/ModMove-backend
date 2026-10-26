using ModMove.Domain.Common;

namespace ModMove.Domain.Entities;

/// <summary>Lookup entry such as a cargo type or incident category (UC-ADM-14..16).</summary>
public sealed class MasterDataItem : BaseAuditableEntity, IAggregateRoot
{
    private MasterDataItem() { }

    public string Category { get; private set; } = null!;
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;

    /// <summary>Long text, e.g. the body of a POLICY entry shown to guests (UC-GST-04, DECISIONS B13).</summary>
    public string? Description { get; private set; }

    public bool IsActive { get; private set; } = true;

    public static MasterDataItem Create(string category, string code, string name, string? description = null) =>
        new() { Category = category, Code = code, Name = name, Description = description };

    public void Rename(string name) => Name = name;

    public void UpdateDescription(string? description) => Description = description;

    public void Update(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    public void SetActive(bool isActive) => IsActive = isActive;
}
