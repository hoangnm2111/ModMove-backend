using ModMove.Domain.Common;
using ModMove.Domain.Constants;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.Domain.Entities;

/// <summary>
/// Refund case from a cancellation, an incident or a complaint, up to the Admin payout.
/// A Dispatcher proposes, an Administrator approves (GB-19) and pays out by hand (GB-27).
/// </summary>
public sealed class Refund : BaseAuditableEntity, IAggregateRoot
{
    private Refund() { }

    public long OrderId { get; private set; }
    public long? IncidentId { get; private set; }
    public RefundSourceType SourceType { get; private set; }
    public string Reason { get; private set; } = null!;
    public List<string>? EvidenceUrls { get; private set; }
    public decimal RequestedAmount { get; private set; }
    public decimal? ProposedAmount { get; private set; }
    public string? ProposalReason { get; private set; }
    public long? ProposedBy { get; private set; }
    public long? ApprovedBy { get; private set; }
    public string? InfoRequestNote { get; private set; }

    /// <summary>The customer's answer to <see cref="InfoRequestNote"/> (UC-DIS-14, DECISIONS 7.4).</summary>
    public string? CustomerReply { get; private set; }

    public RefundStatus RefundStatus { get; private set; } = RefundStatus.Requested;
    public string? BankReferenceCode { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    public static Refund Request(
        long orderId, long? incidentId, RefundSourceType source, string reason,
        IEnumerable<string>? evidenceUrls, decimal requestedAmount) =>
        new()
        {
            OrderId = orderId,
            IncidentId = incidentId,
            SourceType = source,
            Reason = reason,
            EvidenceUrls = evidenceUrls?.ToList(),
            RequestedAmount = requestedAmount,
        };

    public void RequestMoreInfo(string note)
    {
        EnsureStatus(RefundStatus.Requested);
        InfoRequestNote = note;
    }

    /// <summary>
    /// DECISIONS 7.4: the customer answers a dispatcher's request with a text and more photos; the photos join
    /// <see cref="EvidenceUrls"/>, at most 5 in all.
    /// </summary>
    public void ReplyToInfoRequest(string reply, IEnumerable<string>? extraEvidenceUrls)
    {
        EnsureStatus(RefundStatus.Requested);
        if (InfoRequestNote is null)
            throw new DomainException("The dispatcher has not asked for more information.", "NO_INFO_REQUEST");

        var evidence = (EvidenceUrls ?? []).Concat(extraEvidenceUrls ?? []).ToList();
        if (evidence.Count > BusinessRules.MaxRefundEvidencePhotos)
            throw new DomainException($"At most {BusinessRules.MaxRefundEvidencePhotos} photos in all.", "TOO_MANY_PHOTOS");

        CustomerReply = reply;
        EvidenceUrls = evidence;
    }

    /// <summary>UC-DIS-09 / UC-DIS-15: full, partial or no refund (amount 0).</summary>
    public void Propose(decimal amount, string reason, long dispatcherId)
    {
        EnsureStatus(RefundStatus.Requested);
        if (amount < 0 || amount > RequestedAmount)
            throw new DomainException("The proposed amount must be between 0 and the requested amount.");

        ProposedAmount = amount;
        ProposalReason = reason;
        ProposedBy = dispatcherId;
    }

    public void Approve(long adminId, DateTimeOffset now)
    {
        EnsureStatus(RefundStatus.Requested);
        if (ProposedAmount is null)
            throw new DomainException("A refund needs a dispatcher proposal before it can be approved.");

        RefundStatus = RefundStatus.Approved;
        ApprovedBy = adminId;
        DecidedAt = now;
    }

    public void RejectRefund(long adminId, DateTimeOffset now)
    {
        EnsureStatus(RefundStatus.Requested);
        RefundStatus = RefundStatus.Rejected;
        ApprovedBy = adminId;
        DecidedAt = now;
    }

    public void MarkProcessed(string bankReferenceCode, DateTimeOffset now)
    {
        EnsureStatus(RefundStatus.Approved);
        RefundStatus = RefundStatus.Processed;
        BankReferenceCode = bankReferenceCode;
        ProcessedAt = now;
    }

    public void MarkFailed()
    {
        EnsureStatus(RefundStatus.Approved);
        RefundStatus = RefundStatus.Failed;
    }

    /// <summary>UC-CUS-24: the customer withdraws a complaint that is still Requested.</summary>
    public void Withdraw()
    {
        EnsureStatus(RefundStatus.Requested);
        RefundStatus = RefundStatus.Withdrawn;
    }

    private void EnsureStatus(RefundStatus expected)
    {
        if (RefundStatus != expected)
            throw new DomainException($"The refund is {RefundStatus}; expected {expected}.");
    }
}
