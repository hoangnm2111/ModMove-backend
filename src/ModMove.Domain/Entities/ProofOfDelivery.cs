using ModMove.Domain.Common;
using ModMove.Domain.Enums;

namespace ModMove.Domain.Entities;

/// <summary>Cargo evidence at pickup and handover evidence at drop-off (one per order).</summary>
public sealed class ProofOfDelivery : BaseAuditableEntity
{
    private ProofOfDelivery() { }

    public long OrderId { get; private set; }

    /// <summary>At least 2 photos (GB-11).</summary>
    public List<string>? PickupPhotoUrls { get; private set; }

    /// <summary>How many times pickup photos were sent; at most 5 (DECISIONS 6.3).</summary>
    public int PickupSubmissionCount { get; private set; }

    public ProofStatus ProofStatus { get; private set; } = ProofStatus.Pending;
    public string? RejectionReason { get; private set; }
    public string? DeliveryPhotoUrl { get; private set; }
    public DateTimeOffset? DeliveryCompletionTime { get; private set; }

    internal static ProofOfDelivery Create() => new();

    internal void RecordPickup(IEnumerable<string> photoUrls)
    {
        PickupPhotoUrls = [.. photoUrls];
        PickupSubmissionCount++;
        ProofStatus = ProofStatus.Pending;
        RejectionReason = null;
    }

    /// <summary>DECISIONS 7.2: a new driver re-checks the cargo and starts over with all 5 pickup submissions.</summary>
    internal void ResetPickupSubmissions()
    {
        PickupSubmissionCount = 0;
        ProofStatus = ProofStatus.Pending;
        RejectionReason = null;
    }

    internal void Verify() => ProofStatus = ProofStatus.Verified;

    internal void Reject(string reason)
    {
        ProofStatus = ProofStatus.Rejected;
        RejectionReason = reason;
    }

    internal void RecordDelivery(string photoUrl, DateTimeOffset at)
    {
        DeliveryPhotoUrl = photoUrl;
        DeliveryCompletionTime = at;
    }
}
