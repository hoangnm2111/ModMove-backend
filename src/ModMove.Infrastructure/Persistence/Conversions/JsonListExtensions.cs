using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ModMove.Infrastructure.Persistence.Conversions;

/// <summary>
/// Stores a List&lt;string&gt; (photo URLs, rejection reasons) in a MySQL JSON column.
/// EF skips the converter for null, so nullable lists map to SQL NULL.
/// </summary>
internal static class JsonListExtensions
{
    public static PropertyBuilder HasJsonList(this PropertyBuilder property) =>
        property.HasColumnType("json").HasConversion<JsonListConverter, JsonListComparer>();
}

internal sealed class JsonListConverter() : ValueConverter<List<string>, string>(
    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

internal sealed class JsonListComparer() : ValueComparer<List<string>>(
    (a, b) => a != null && b != null && a.SequenceEqual(b),
    v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
    v => v.ToList());
