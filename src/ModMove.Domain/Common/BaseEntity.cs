namespace ModMove.Domain.Common;

/// <summary>
/// Base class for every entity. Id is a database-generated BIGINT (SDS section 3).
/// Equality is by identity; a not-yet-saved entity (Id == 0) is only equal to itself.
/// </summary>
public abstract class BaseEntity
{
    public long Id { get; protected set; }

    public override bool Equals(object? obj) =>
        ReferenceEquals(this, obj) ||
        (obj is BaseEntity other && GetType() == other.GetType() && Id != 0 && Id == other.Id);

    public override int GetHashCode() =>
        Id == 0 ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this) : HashCode.Combine(GetType(), Id);
}
