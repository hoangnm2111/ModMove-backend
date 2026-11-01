using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using ModMove.Application.Common.Interfaces;

namespace ModMove.Infrastructure.ExternalServices.Cloudinary;

/// <summary>
/// Signed uploads to the team's Cloudinary (GB-06). The size and format are checked by the use case before this is
/// called; Cloudinary is asked to refuse anything but JPG/PNG as a second line.
/// </summary>
public sealed class CloudinaryFileStorage(CloudinaryOptions options) : IFileStorage
{
    private readonly CloudinaryDotNet.Cloudinary _cloudinary = new(new Account(options.CloudName, options.ApiKey, options.ApiSecret))
    {
        Api = { Secure = true },
    };

    public async Task<string> UploadImageAsync(Stream content, string fileName, string folder, CancellationToken cancellationToken)
    {
        var result = await _cloudinary.UploadAsync(new ImageUploadParams
        {
            File = new FileDescription(fileName, content),
            UploadPreset = options.UploadPreset,
            Folder = folder,
            AllowedFormats = ["jpg", "png"],
        }, cancellationToken);

        if (result.Error is not null || result.SecureUrl is null)
            throw new InvalidOperationException($"Cloudinary upload failed: {result.Error?.Message}");

        return result.SecureUrl.ToString();
    }

    public bool IsOwnedUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        string.Equals(uri.Host, "res.cloudinary.com", StringComparison.OrdinalIgnoreCase) &&
        uri.AbsolutePath.StartsWith($"/{options.CloudName}/", StringComparison.Ordinal);
}
