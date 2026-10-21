using ModMove.Domain.Common;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.Domain.Entities;

/// <summary>
/// A guest's question before registering (UC-GST-05, DECISIONS B14). Staff answer it outside the
/// system (phone, message); the system only records it and whether it was handled.
/// </summary>
public sealed class SupportRequest : BaseAuditableEntity, IAggregateRoot
{
    private SupportRequest() { }

    public string FullName { get; private set; } = null!;

    /// <summary>Phone number or email to call back.</summary>
    public string Contact { get; private set; } = null!;

    public string Content { get; private set; } = null!;
    public SupportRequestStatus Status { get; private set; } = SupportRequestStatus.New;

    public static SupportRequest Submit(string fullName, string contact, string content)
    {
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(contact) || string.IsNullOrWhiteSpace(content))
            throw new DomainException("Name, contact and question are all required.");

        return new SupportRequest { FullName = fullName.Trim(), Contact = contact.Trim(), Content = content.Trim() };
    }

    public void MarkHandled() => Status = SupportRequestStatus.Handled;
}
