using System.Text.Json;
using ModMove.Domain.Constants;

namespace ModMove.Application.Features.DriverOnboarding;

/// <summary>Structured rejection entry returned on both Driver status and Admin detail endpoints.</summary>
public sealed record RejectedApplicationItemDto(string Key, string Reason);

internal static class DriverOnboardingJson
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    public static List<string> ParseList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, WebJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static List<string>? ParseNullableList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, WebJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static List<RejectedApplicationItemDto>? ParseRejectedItems(
        string? json,
        IReadOnlyList<string>? fallbackReasons = null)
    {
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<RejectedApplicationItemDto>>(json, WebJson);
                if (parsed is { Count: > 0 })
                    return parsed;
            }
            catch (JsonException)
            {
                // Fall back to reason inference below.
            }
        }

        if (fallbackReasons is null || fallbackReasons.Count == 0)
            return null;

        return fallbackReasons.Select(InferFromReasonText).ToList();
    }

    public static RejectedApplicationItemDto InferFromReasonText(string raw)
    {
        var text = raw.Trim();
        var colonIndex = text.IndexOf(':');
        if (colonIndex > 0)
        {
            var prefix = text[..colonIndex].Trim();
            var rest = text[(colonIndex + 1)..].Trim();
            if (DriverApplicationItemKeys.Normalize(prefix) is { } canonicalKey)
                return new RejectedApplicationItemDto(canonicalKey, string.IsNullOrEmpty(rest) ? text : rest);
        }

        var lower = text.ToLowerInvariant();
        if (lower.Contains("cccd") || lower.Contains("căn cước") || lower.Contains("citizen"))
        {
            var key = lower.Contains("sau") || lower.Contains("back")
                ? DriverApplicationItemKeys.CitizenIdBack
                : DriverApplicationItemKeys.CitizenIdFront;
            return new RejectedApplicationItemDto(key, text);
        }

        if (lower.Contains("gplx") || lower.Contains("giấy phép") || lower.Contains("bằng lái") || lower.Contains("licen"))
        {
            var key = lower.Contains("sau") || lower.Contains("back")
                ? DriverApplicationItemKeys.LicenseBack
                : DriverApplicationItemKeys.LicenseFront;
            return new RejectedApplicationItemDto(key, text);
        }

        if (lower.Contains("đăng ký") || lower.Contains("cà vẹt") || lower.Contains("registration"))
            return new RejectedApplicationItemDto(DriverApplicationItemKeys.VehicleRegistration, text);

        if (lower.Contains("đăng kiểm") || lower.Contains("inspection"))
            return new RejectedApplicationItemDto(DriverApplicationItemKeys.VehicleInspection, text);

        if (lower.Contains("ảnh xe") || lower.Contains("hình xe") || lower.Contains("vehicle photo"))
            return new RejectedApplicationItemDto(DriverApplicationItemKeys.VehiclePhoto, text);

        if (lower.Contains("chân dung") || lower.Contains("portrait"))
            return new RejectedApplicationItemDto(DriverApplicationItemKeys.Portrait, text);

        if (lower.Contains("biển số") || lower.Contains("tải trọng") || lower.Contains("loại xe") || lower.Contains("plate"))
            return new RejectedApplicationItemDto(DriverApplicationItemKeys.VehicleInfo, text);

        return new RejectedApplicationItemDto(DriverApplicationItemKeys.Other, text);
    }
}

