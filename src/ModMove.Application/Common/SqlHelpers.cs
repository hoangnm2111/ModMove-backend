namespace ModMove.Application.Common;

/// <summary>Small helpers shared by the Dapper query handlers.</summary>
internal static class SqlHelpers
{
    /// <summary>SQL IN-list matching <see cref="Domain.Constants.OrderStatusGroups.WithDriver"/>.</summary>
    public const string WithDriverStatusSql = "'Accepted', 'Arrived', 'PickedUp', 'InTransit'";

    /// <summary>
    /// MySQL returns DATETIME without a zone; the database holds UTC, so mark it as UTC to serialize as ISO 8601 UTC.
    /// </summary>
    public static DateTimeOffset ToUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public static DateTimeOffset? ToUtc(DateTime? value) => value is null ? null : ToUtc(value.Value);

    /// <summary>
    /// A LIKE pattern that matches rows containing <paramref name="search"/> as plain text: <c>%</c>, <c>_</c> and
    /// <c>\</c> typed by the user are escaped. Null or blank gives null, which the SQL treats as "no filter".
    /// </summary>
    public static string? LikeContains(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return null;

        var escaped = search.Trim().Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
        return $"%{escaped}%";
    }

    /// <summary>
    /// Deserializes a JSON array of strings stored in MySQL JSON columns (photo URLs, etc.).
    /// Returns an empty list if null, whitespace, or invalid.
    /// </summary>
    public static List<string> ParseJsonList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
