using FluentValidation.Results;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;

namespace ModMove.Application.Common;

/// <summary>A value the client picks by code from api/public/lookups: a cargo type, a complaint or a cancellation reason.</summary>
internal static class MasterDataChoice
{
    public const int MaxCodeLength = 50; // master_data_items.code

    /// <summary>Unknown or switched-off code is a 400 on <paramref name="field"/>, like any other bad input.</summary>
    public static async Task<MasterDataItem> RequireActiveAsync(
        this IMasterDataRepository items, string category, string code, string field, CancellationToken cancellationToken) =>
        await items.GetActiveAsync(category, code.Trim(), cancellationToken)
        ?? throw new ValidationException([new ValidationFailure(field, $"'{code}' is not in the list of {category}.")]);

    /// <summary>
    /// What the customer wrote, or the name of the entry they picked. The "Khác" entry says nothing by itself, so it needs
    /// a written reason. Checked on the entry found: the database matches codes ignoring case and accents, the input may differ.
    /// </summary>
    public static string ReasonText(this MasterDataItem item, string? written)
    {
        if (!string.IsNullOrWhiteSpace(written))
            return written.Trim();
        if (string.Equals(item.Code, "OTHER", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException([new ValidationFailure("Reason", "Write the reason when choosing OTHER.")]);
        return item.Name;
    }
}
