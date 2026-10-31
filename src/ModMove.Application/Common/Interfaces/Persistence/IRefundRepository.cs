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
}
