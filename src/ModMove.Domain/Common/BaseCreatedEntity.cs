namespace ModMove.Domain.Common;

/// <summary>
/// Entity of an append-only table: only created_at. Filled by the Infrastructure
/// SaveChanges interceptor; handlers never set it by hand.
/// </summary>
public abstract class BaseCreatedEntity : BaseEntity
{
    public DateTimeOffset CreatedAt { get; set; }
}
