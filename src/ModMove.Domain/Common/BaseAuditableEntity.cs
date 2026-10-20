namespace ModMove.Domain.Common;

/// <summary>
/// Entity with created_at / updated_at. Values are filled automatically by the
/// Infrastructure SaveChanges interceptor; handlers never set them by hand.
/// </summary>
public abstract class BaseAuditableEntity : BaseCreatedEntity
{
    public DateTimeOffset? UpdatedAt { get; set; }
}
