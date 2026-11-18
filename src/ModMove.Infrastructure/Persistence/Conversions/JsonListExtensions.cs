using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ModMove.Infrastructure.Persistence.Conversions;

/// <summary>
/// Stores a List&lt;T&gt; (photo URLs, rejection reasons, structured rejected items) in a MySQL JSON column.
/// EF skips the converter for null, so nullable lists map to SQL NULL.
/// </summary>
internal static class JsonListExtensions
{
    internal static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    public static PropertyBuilder HasJsonList(this PropertyBuilder property) =>
        property.HasColumnType("json").HasConversion<JsonListConverter, JsonListComparer>();

    public static PropertyBuilder HasJsonList<T>(this PropertyBuilder property) =>
        property.HasColumnType("json").HasConversion<JsonTypedListConverter<T>, JsonTypedListComparer<T>>();
}

internal sealed class JsonTypedListConverter<T>() : ValueConverter<List<T>, string>(
    v => JsonSerializer.Serialize(v, JsonListExtensions.WebJson),
    v => JsonSerializer.Deserialize<List<T>>(v, JsonListExtensions.WebJson) ?? new List<T>());

internal sealed class JsonTypedListComparer<T>() : ValueComparer<List<T>>(
    (a, b) => a != null && b != null && a.SequenceEqual(b),
    v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item == null ? 0 : item.GetHashCode())),
    v => v.ToList());

internal sealed class JsonListConverter() : ValueConverter<List<string>, string>(
    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

internal sealed class JsonListComparer() : ValueComparer<List<string>>(
    (a, b) => a != null && b != null && a.SequenceEqual(b),
    v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
    v => v.ToList());
