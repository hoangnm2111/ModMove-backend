namespace ModMove.Domain.Common;

/// <summary>
/// Marks the root of a consistency boundary. Only aggregate roots get a
/// repository; child entities are changed through their root.
/// </summary>
public interface IAggregateRoot;
