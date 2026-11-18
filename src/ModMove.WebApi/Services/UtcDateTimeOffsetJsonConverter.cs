using System.Text.Json;
using System.Text.Json.Serialization;
using ModMove.Domain.Enums;

namespace ModMove.WebApi.Services;

/// <summary>
/// Serializes every <see cref="DateTimeOffset"/> as ISO 8601 UTC with a trailing <c>Z</c>
/// instead of <c>+00:00</c> or a local host offset.
/// </summary>
public sealed class UtcDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTimeOffset().ToUniversalTime();

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.UtcDateTime);
}

/// <summary>
/// Accepts both PascalCase (<c>WrongBankAccount</c>, <c>Other</c>) and SCREAMING_SNAKE_CASE (<c>WRONG_BANK_ACCOUNT</c>, <c>OTHER</c>).
/// </summary>
public sealed class PayoutRejectionReasonCodeJsonConverter : JsonConverter<PayoutRejectionReasonCode>
{
    public override PayoutRejectionReasonCode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var num) && Enum.IsDefined(typeof(PayoutRejectionReasonCode), num))
            return (PayoutRejectionReasonCode)num;

        var raw = reader.GetString()?.Trim();
        if (string.IsNullOrEmpty(raw))
            return PayoutRejectionReasonCode.Other;

        var normalized = raw.Replace("_", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal);
        if (Enum.TryParse<PayoutRejectionReasonCode>(normalized, ignoreCase: true, out var parsed))
            return parsed;

        throw new JsonException($"Invalid PayoutRejectionReasonCode '{raw}'. Expected WrongBankAccount or Other.");
    }

    public override void Write(Utf8JsonWriter writer, PayoutRejectionReasonCode value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}

