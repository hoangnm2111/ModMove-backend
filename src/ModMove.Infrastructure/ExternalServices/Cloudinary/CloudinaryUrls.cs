using System.Text.RegularExpressions;

namespace ModMove.Infrastructure.ExternalServices.Cloudinary;

/// <summary>Reads and cleans Cloudinary delivery URLs of private ("authenticated") photos (NFR 2.3).</summary>
internal static partial class CloudinaryUrls
{
    public const string AuthenticatedType = "authenticated";

    /// <summary>Drops the <c>s--…--</c> signature, without which an authenticated URL answers 401.</summary>
    public static string WithoutSignature(string url) => SignatureSegment().Replace(url, "/", 1);

    /// <summary>
    /// <c>https://res.cloudinary.com/{cloud}/image/authenticated/[s--sig--/]v123/{public id}.{format}</c> → public id and format.
    /// Anything else (a public photo, another cloud) → false.
    /// </summary>
    public static bool TryParseAuthenticated(string url, string cloudName, out string publicId, out string format)
    {
        publicId = "";
        format = "";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Host, "res.cloudinary.com", StringComparison.OrdinalIgnoreCase))
            return false;

        var match = AuthenticatedPath().Match(uri.AbsolutePath);
        if (!match.Success || !string.Equals(match.Groups["cloud"].Value, cloudName, StringComparison.Ordinal))
            return false;

        publicId = Uri.UnescapeDataString(match.Groups["id"].Value);
        format = match.Groups["format"].Value;
        return true;
    }

    [GeneratedRegex("/s--[^/]+--/")]
    private static partial Regex SignatureSegment();

    [GeneratedRegex(@"^/(?<cloud>[^/]+)/image/authenticated/(?:s--[^/]+--/)?(?:v\d+/)?(?<id>.+)\.(?<format>[A-Za-z0-9]+)$")]
    private static partial Regex AuthenticatedPath();
}
