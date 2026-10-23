using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.MasterData.Queries.GetPublicContent;

/// <summary>Active master data entries of one category, with their text (policies UC-GST-04, service info UC-GST-03).</summary>
public sealed record GetPublicContentQuery(string Category) : IQuery<IReadOnlyList<ContentResponse>>;

public sealed record ContentResponse(string Code, string Name, string? Description);

/// <summary>The categories guests may read. Controllers pick one; the client never sends a category.</summary>
public static class PublicContentCategories
{
    public const string Policy = "POLICY";
    public const string ServiceInfo = "SERVICE_INFO";
}
