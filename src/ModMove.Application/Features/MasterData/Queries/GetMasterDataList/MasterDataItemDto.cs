namespace ModMove.Application.Features.MasterData.Queries.GetMasterDataList;

public sealed record MasterDataItemDto
{
    public long Id { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }

    public MasterDataItemDto() { }

    public MasterDataItemDto(
        long id,
        string category,
        string code,
        string name,
        string? description,
        bool isActive,
        DateTimeOffset createdAt,
        DateTimeOffset? updatedAt)
    {
        Id = id;
        Category = category;
        Code = code;
        Name = name;
        Description = description;
        IsActive = isActive;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }
}
