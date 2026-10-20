using ModMove.Domain.Common;
using ModMove.Domain.Enums;

namespace ModMove.Domain.Entities;

/// <summary>Append-only log of every order status change.</summary>
public sealed class OrderStatusHistory : BaseCreatedEntity
{
    private OrderStatusHistory() { }

    public long OrderId { get; private set; }
    public OrderStatus? FromStatus { get; private set; }
    public OrderStatus ToStatus { get; private set; }
    public ChangedByRole ChangedByRole { get; private set; }

    /// <summary>Null for System jobs.</summary>
    public long? ChangedByUserId { get; private set; }

    public string? Note { get; private set; }

    internal static OrderStatusHistory Create(OrderStatus? from, OrderStatus to, ChangedByRole role, long? userId, string? note) =>
        new() { FromStatus = from, ToStatus = to, ChangedByRole = role, ChangedByUserId = userId, Note = note };
}
