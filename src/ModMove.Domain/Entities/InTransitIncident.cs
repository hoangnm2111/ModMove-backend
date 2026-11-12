using ModMove.Domain.Common;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.Domain.Entities;

/// <summary>A problem reported during a trip and handled by a Dispatcher (GB-17).</summary>
public sealed class InTransitIncident : BaseAuditableEntity, IAggregateRoot
{
    private InTransitIncident() { }

    public long OrderId { get; private set; }
    public long ReportedBy { get; private set; }
    public IncidentType IncidentType { get; private set; }
    public string Description { get; private set; } = null!;
    public List<string>? PhotoUrls { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public IncidentResolutionStatus ResolutionStatus { get; private set; } = IncidentResolutionStatus.Open;
    public IncidentDecision? ResolutionDecision { get; private set; }
    public string? ResolutionNote { get; private set; }
    public long? HandledBy { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>Open or InProgress: still waiting for a Dispatcher's decision. A driver's open incident holds the trip (DECISIONS 7.6).</summary>
    public bool IsOpen => ResolutionStatus is IncidentResolutionStatus.Open or IncidentResolutionStatus.InProgress;

    public static InTransitIncident Report(
        long orderId, long reportedBy, IncidentType type, string description,
        IEnumerable<string>? photoUrls, decimal? latitude, decimal? longitude) =>
        new()
        {
            OrderId = orderId,
            ReportedBy = reportedBy,
            IncidentType = type,
            Description = description,
            PhotoUrls = photoUrls?.ToList(),
            Latitude = latitude,
            Longitude = longitude,
        };

    public void StartHandling(long dispatcherId)
    {
        EnsureOpen();
        HandledBy = dispatcherId;
        ResolutionStatus = IncidentResolutionStatus.InProgress;
    }

    /// <summary>
    /// DECISIONS Q10: the Dispatcher holding the incident gives it back (end of shift, account locked) so another one can take
    /// it. A driver's incident still holds the trip (7.6) until someone decides.
    /// </summary>
    public void Release(long dispatcherId)
    {
        if (ResolutionStatus != IncidentResolutionStatus.InProgress || HandledBy != dispatcherId)
            throw new DomainException("Only the Dispatcher handling an incident can give it back.");
        HandledBy = null;
        ResolutionStatus = IncidentResolutionStatus.Open;
    }

    public void Resolve(IncidentDecision decision, string? note, long dispatcherId, DateTimeOffset now)
    {
        EnsureOpen();
        ResolutionDecision = decision;
        ResolutionNote = note;
        HandledBy = dispatcherId;
        ResolvedAt = now;
        ResolutionStatus = IncidentResolutionStatus.Resolved;
    }

    /// <summary>UC-DIS-10: cancel an incident that has no valid reason.</summary>
    public void CancelIncident(string note, long dispatcherId, DateTimeOffset now)
    {
        EnsureOpen();
        ResolutionNote = note;
        HandledBy = dispatcherId;
        ResolvedAt = now;
        ResolutionStatus = IncidentResolutionStatus.Cancelled;
    }

    /// <summary>
    /// DECISIONS 7.6: the customer cancelled the order (Pending/Accepted), so nothing is left for a Dispatcher to decide.
    /// No Dispatcher handled it, so <see cref="HandledBy"/> is cleared.
    /// </summary>
    public void CloseBecauseOrderCancelled(string note, DateTimeOffset now)
    {
        EnsureOpen();
        ResolutionNote = note;
        HandledBy = null;
        ResolvedAt = now;
        ResolutionStatus = IncidentResolutionStatus.Cancelled;
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
            throw new DomainException("The incident is already closed.");
    }
}
