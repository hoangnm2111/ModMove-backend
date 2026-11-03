using ModMove.Domain.Entities;

namespace ModMove.Application.Common.Interfaces.Persistence;

public interface IRefundRepository : IRepository<Refund>
{
    /// <summary>
    /// DECISIONS B9: one open complaint per order. Open = Requested (being reviewed) or Approved (waiting for payout);
    /// Rejected, Processed, Failed and Withdrawn are closed.
    /// </summary>
    Task<bool> HasOpenComplaintAsync(long orderId, CancellationToken cancellationToken = default);

    /// <summary>DECISIONS B9: one open refund per incident (Requested or Approved).</summary>
    Task<bool> HasOpenRefundForIncidentAsync(long incidentId, CancellationToken cancellationToken = default);

    /// <summary>The refund still open (Requested or Approved) for an incident, or null; the Dispatcher works on it (UC-DIS-08, 14).</summary>
    Task<Refund?> GetOpenForIncidentAsync(long incidentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// All refunds still open (Requested or Approved) on the order across incidents, complaints and cancellation.
    /// Used when cancelling an order due to an incident to adjust open claims and check limits.
    /// </summary>
    Task<List<Refund>> GetOpenForOrderAsync(long orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// DECISIONS B9: what the order's refunds add up to so far: open or paid out, at the proposed amount once a Dispatcher
    /// proposed one. Rejected, Failed and Withdrawn refunds paid nothing and do not count.
    /// </summary>
    Task<decimal> GetClaimedTotalForOrderAsync(long orderId, CancellationToken cancellationToken = default);

    /// <summary>DECISIONS B8: whether any order of the customer has a refund still open (Requested or Approved).</summary>
    Task<bool> HasOpenForCustomerAsync(long customerId, CancellationToken cancellationToken = default);
}
