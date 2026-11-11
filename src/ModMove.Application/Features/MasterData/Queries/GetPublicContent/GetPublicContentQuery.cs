using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.MasterData.Queries.GetPublicContent;

/// <summary>Active master data entries of one category, with their text (policies UC-GST-04, service info UC-GST-03, lists to pick a code from).</summary>
public sealed record GetPublicContentQuery(string Category) : IQuery<IReadOnlyList<ContentResponse>>;

public sealed record ContentResponse
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }

    public ContentResponse() { }

    public ContentResponse(string code, string name, string? description)
    {
        Code = code;
        Name = name;
        Description = description;
    }
}

/// <summary>The categories guests may read. Controllers pick one; the client never sends a category.</summary>
public static class PublicContentCategories
{
    public const string Policy = "POLICY";
    public const string ServiceInfo = "SERVICE_INFO";
    public const string CargoType = "CARGO_TYPE";
    public const string ComplaintReason = "COMPLAINT_REASON";
    public const string CancellationReason = "CANCELLATION_REASON";

    /// <summary>R-01: the banks a customer picks to receive a refund; code = 6-digit BIN.</summary>
    public const string Bank = "BANK";
}
