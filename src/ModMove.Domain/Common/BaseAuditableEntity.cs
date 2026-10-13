namespace ModMove.Domain.Common;

/// <summary>
/// Entity with audit fields. Values are filled automatically by the
/// Infrastructure SaveChanges interceptor; handlers never set them by hand.
/// </summary>
public abstract class BaseAuditableEntity : BaseEntity
{
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
