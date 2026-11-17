using Microsoft.Extensions.Logging;
using ModMove.Application.Common.Interfaces;
using ModMove.Domain.Exceptions;

namespace ModMove.Infrastructure.ExternalServices.Cloudinary;

/// <summary>
/// Stands in when Cloudinary:CloudName is not set, so the app still starts. Uploads fail with UPLOAD_FAILED (422)
/// and no image URL counts as ours, so orders with photos are refused until Cloudinary is configured.
/// </summary>
public sealed class NotConfiguredFileStorage(ILogger<NotConfiguredFileStorage>? logger = null) : IFileStorage
{
    public Task<string> UploadImageAsync(Stream content, string fileName, string folder, CancellationToken cancellationToken)
    {
        logger?.LogError("Cloudinary upload failed: Cloudinary:CloudName is not configured.");
        throw new DomainException(
            "Cloudinary is not configured: set the Cloudinary section in appsettings.Development.json.",
            "UPLOAD_FAILED");
    }

    public bool IsOwnedUrl(string url) => false;
}
