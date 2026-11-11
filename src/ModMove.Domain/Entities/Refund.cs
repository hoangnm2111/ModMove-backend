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

    /// <summary>The list code behind <see cref="Reason"/> (COMPLAINT_REASON or CANCELLATION_REASON); null for an incident refund.</summary>
    public string? ReasonCode { get; private set; }

    public List<string>? EvidenceUrls { get; private set; }
    public decimal RequestedAmount { get; private set; }
    public decimal? ProposedAmount { get; private set; }

    /// <summary>The Dispatcher's internal note for the Administrator; never shown to the customer.</summary>
    public string? ProposalReason { get; private set; }

    public long? ProposedBy { get; private set; }
    public long? ApprovedBy { get; private set; }

    /// <summary>UC-CUS-23: the Administrator's reason, which the customer sees.</summary>
    public string? DecisionNote { get; private set; }

    public string? InfoRequestNote { get; private set; }

    /// <summary>The customer's answer to <see cref="InfoRequestNote"/> (UC-DIS-14, DECISIONS 7.4).</summary>
    public string? CustomerReply { get; private set; }

    public RefundStatus RefundStatus { get; private set; } = RefundStatus.Requested;
    public string? BankReferenceCode { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    // R-01: where the Administrator sends the money. A snapshot on this refund, not on the user.
    public string? PayoutBankCode { get; private set; }      // master data BANK code = 6-digit BIN
    public string? PayoutBankName { get; private set; }      // name at the time it was given
    public string? PayoutAccountNumber { get; private set; }
    public string? PayoutAccountHolder { get; private set; } // upper case, no accents
    public DateTimeOffset? PayoutInfoUpdatedAt { get; private set; }

    /// <summary>Why the Administrator's last transfer failed; cleared when the customer fixes the account.</summary>
    public string? PayoutFailureReason { get; private set; }

    public bool HasPayoutAccount => PayoutAccountNumber is not null;

    /// <summary>Optimistic lock: of a customer withdrawing and an Administrator deciding at once, the later save is a 409.</summary>
    public int RowVersion { get; private set; }

    /// <summary>
    /// DECISIONS B9: a cancellation refund skips the Dispatcher, so it is proposed at its full amount on creation and
    /// an Administrator can approve it at once. Incident and complaint refunds wait for <see cref="Propose"/>.
    /// </summary>
    public static Refund Request(
        long orderId, long? incidentId, RefundSourceType source, string reason,
        IEnumerable<string>? evidenceUrls, decimal requestedAmount, string? reasonCode = null) =>
        new()
        {
            OrderId = orderId,
            IncidentId = incidentId,
            SourceType = source,
            Reason = reason,
            ReasonCode = reasonCode,
            EvidenceUrls = evidenceUrls?.ToList(),
            RequestedAmount = requestedAmount,
            ProposedAmount = source == RefundSourceType.Cancellation ? requestedAmount : null,
        };

    public void RequestMoreInfo(string note)
    {
        EnsureStatus(RefundStatus.Requested);
        EnsureDispatcherStep();
        InfoRequestNote = note;
        CustomerReply = null; // a new question waits for a new answer (7.4)
        RowVersion++;
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
        RowVersion++;
    }

    /// <summary>UC-DIS-09 / UC-DIS-15: full, partial or no refund (amount 0).</summary>
    public void Propose(decimal amount, string reason, long dispatcherId)
    {
        EnsureStatus(RefundStatus.Requested);
        EnsureDispatcherStep();
        if (amount < 0 || amount > RequestedAmount)
            throw new DomainException("The proposed amount must be between 0 and the requested amount.");

        ProposedAmount = amount;
        ProposalReason = reason;
        ProposedBy = dispatcherId;
        RowVersion++;
    }

    public void Approve(long adminId, DateTimeOffset now, string? note = null)
    {
        EnsureStatus(RefundStatus.Requested);
        if (ProposedAmount is null)
            throw new DomainException("A refund needs a dispatcher proposal before it can be approved.");

        RefundStatus = RefundStatus.Approved;
        ApprovedBy = adminId;
        DecidedAt = now;
        DecisionNote = note;
        RowVersion++;
    }

    public void RejectRefund(long adminId, DateTimeOffset now, string? note = null)
    {
        EnsureStatus(RefundStatus.Requested);
        RefundStatus = RefundStatus.Rejected;
        ApprovedBy = adminId;
        DecidedAt = now;
        DecisionNote = note;
        RowVersion++;
    }

    public void MarkProcessed(string bankReferenceCode, DateTimeOffset now)
    {
        EnsureStatus(RefundStatus.Approved);
        if (!HasPayoutAccount)
            throw new DomainException("The customer has not given a bank account to receive this refund.", "REFUND_BANK_ACCOUNT_REQUIRED");

        RefundStatus = RefundStatus.Processed;
        BankReferenceCode = bankReferenceCode;
        ProcessedAt = now;
        RowVersion++;
    }

    /// <summary>R-01: the Administrator's transfer failed; the customer fixes the account (<see cref="SetPayoutAccount"/>).</summary>
    public void MarkFailed(string reason)
    {
        EnsureStatus(RefundStatus.Approved);
        RefundStatus = RefundStatus.Failed;
        PayoutFailureReason = reason;
        RowVersion++;
    }

    /// <summary>R-01: Requested/Approved/Failed; Failed goes back to Approved so the Admin can pay again (UC-ADM-18 5a).</summary>
    public void SetPayoutAccount(string bankCode, string bankName, string accountNumber, string accountHolder, DateTimeOffset now)
    {
        if (RefundStatus is RefundStatus.Processed or RefundStatus.Rejected or RefundStatus.Withdrawn)
            throw new DomainException("This refund is closed; its payout account can no longer change.", "REFUND_PAYOUT_LOCKED");

        // The same account again would send a refund the bank just refused back to the Administrator, over and over.
        if (RefundStatus == RefundStatus.Failed
            && (PayoutBankCode, PayoutAccountNumber, PayoutAccountHolder) == (bankCode, accountNumber, accountHolder))
            throw new DomainException("The transfer failed with this account; give a different one.", "REFUND_PAYOUT_UNCHANGED");

        (PayoutBankCode, PayoutBankName, PayoutAccountNumber, PayoutAccountHolder) = (bankCode, bankName, accountNumber, accountHolder);
        PayoutInfoUpdatedAt = now;
        if (RefundStatus == RefundStatus.Failed)
        {
            RefundStatus = RefundStatus.Approved;
            PayoutFailureReason = null;
        }

        RowVersion++;
    }

    /// <summary>UC-CUS-24: the customer drops a complaint or incident refund still under review. A cancellation refund is owed, so it stays.</summary>
    public void Withdraw()
    {
        if (SourceType == RefundSourceType.Cancellation)
            throw new DomainException("A cancellation refund cannot be withdrawn.");
        EnsureStatus(RefundStatus.Requested);
        RefundStatus = RefundStatus.Withdrawn;
        RowVersion++;
    }

    // DECISIONS B9: a cancellation refund is 100% of what was paid and skips the Dispatcher entirely.
    private void EnsureDispatcherStep()
    {
        if (SourceType == RefundSourceType.Cancellation)
            throw new DomainException("A cancellation refund goes straight to an Administrator.", "REFUND_NOT_FOR_DISPATCHER");
    }

    private void EnsureStatus(RefundStatus expected)
    {
        if (RefundStatus != expected)
            throw new DomainException($"The refund is {RefundStatus}; expected {expected}.");
    }
}
