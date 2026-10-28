using ModMove.Application.Common.Interfaces;

namespace ModMove.Infrastructure.ExternalServices.Cloudinary;

/// <summary>
/// Stands in when Cloudinary:CloudName is not set, so the app still starts. Uploads fail with a clear server error and
/// no image URL counts as ours, so orders with photos are refused until Cloudinary is configured.
/// </summary>
public sealed class NotConfiguredFileStorage : IFileStorage
{
    public Task<string> UploadImageAsync(Stream content, string fileName, string folder, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Cloudinary is not configured: set the Cloudinary section in appsettings.Development.json.");

    public bool IsOwnedUrl(string url) => false;
}
