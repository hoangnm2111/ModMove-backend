using System.Text.RegularExpressions;
using System.Web;

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

    /// <summary>
    /// A signed download link of our cloud, as <c>CreateViewUrl</c> makes it
    /// (<c>https://api.cloudinary.com/v1_1/{cloud}/image/download?public_id=..&amp;format=..&amp;type=authenticated&amp;..</c>) → public id and format.
    /// The signature and expiry are not checked: the link only names the photo.
    /// </summary>
    public static bool TryParseDownload(string url, string cloudName, out string publicId, out string format)
    {
        publicId = "";
        format = "";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.Equals(uri.Host, "api.cloudinary.com", StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath != $"/v1_1/{cloudName}/image/download")
            return false;

        var query = HttpUtility.ParseQueryString(uri.Query);
        if (query["type"] != AuthenticatedType
            || query["public_id"] is not { Length: > 0 } id
            || query["format"] is not { Length: > 0 } extension
            || !extension.All(char.IsAsciiLetterOrDigit))
            return false;

        publicId = id;
        format = extension;
        return true;
    }

    /// <summary>The unsigned delivery URL we save for a private photo.</summary>
    public static string StoredUrl(string cloudName, string publicId, string format) =>
        $"https://res.cloudinary.com/{cloudName}/image/{AuthenticatedType}/{string.Join('/', publicId.Split('/').Select(Uri.EscapeDataString))}.{format}";

    [GeneratedRegex("/s--[^/]+--/")]
    private static partial Regex SignatureSegment();

    [GeneratedRegex(@"^/(?<cloud>[^/]+)/image/authenticated/(?:s--[^/]+--/)?(?:v\d+/)?(?<id>.+)\.(?<format>[A-Za-z0-9]+)$")]
    private static partial Regex AuthenticatedPath();
}
