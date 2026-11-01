namespace ModMove.Domain.Enums;

/// <summary>
/// Operational stage of a customer complaint (Refund with SourceType Complaint) in the Dispatcher console.
/// Inferred from the refund fields without altering the database schema.
/// </summary>
public enum ComplaintStage
{
    /// <summary>New complaint awaiting initial dispatcher review (no proposal, no info request).</summary>
    NeedsReview,

    /// <summary>Dispatcher requested additional information from the customer; awaiting reply.</summary>
    WaitingForCustomer,

    /// <summary>Customer submitted a reply to the information request; ready for proposal.</summary>
    CustomerReplied,

    /// <summary>Dispatcher submitted a proposed resolution/refund; awaiting Administrator approval.</summary>
    ProposedToAdmin,

    /// <summary>Complaint reached a terminal status (Approved, Rejected, Processed, Failed, Withdrawn).</summary>
    Closed,
}
