using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Logging;
using ModMove.Application.Common.Interfaces;
using ModMove.Domain.Constants;
using ModMove.Domain.Exceptions;

namespace ModMove.Infrastructure.ExternalServices.Cloudinary;

/// <summary>
/// Signed uploads to the team's Cloudinary (GB-06). The size and format are checked by the use case before this is
/// called; Cloudinary is asked to refuse anything but JPG/PNG as a second line.
/// </summary>
public sealed class CloudinaryFileStorage(
    CloudinaryOptions options,
    ILogger<CloudinaryFileStorage>? logger = null) : IFileStorage
{
    private readonly CloudinaryDotNet.Cloudinary _cloudinary = new(new Account(options.CloudName, options.ApiKey, options.ApiSecret))
    {
        Api = { Secure = true },
    };

    public Task<string> UploadImageAsync(Stream content, string fileName, string folder, CancellationToken cancellationToken) =>
        UploadAsync(content, fileName, folder, isPrivate: false, cancellationToken);

    /// <summary>NFR 2.3: stored as an "authenticated" asset; the signature is dropped so the saved URL cannot open the photo.</summary>
    public async Task<string> UploadPrivateImageAsync(Stream content, string fileName, string folder, CancellationToken cancellationToken) =>
        CloudinaryUrls.WithoutSignature(await UploadAsync(content, fileName, folder, isPrivate: true, cancellationToken));

    /// <summary>A private photo of our cloud → a signed download link valid <see cref="BusinessRules.DocumentViewLinkLifetime"/>.</summary>
    public string CreateViewUrl(string storedUrl)
    {
        if (!CloudinaryUrls.TryParseAuthenticated(storedUrl, options.CloudName, out var publicId, out var format))
            return storedUrl;

        var expiresAt = DateTimeOffset.UtcNow.Add(BusinessRules.DocumentViewLinkLifetime).ToUnixTimeSeconds();
        return _cloudinary.DownloadPrivate(publicId, attachment: false, format: format, type: CloudinaryUrls.AuthenticatedType, expiresAt: expiresAt);
    }

    private async Task<string> UploadAsync(Stream content, string fileName, string folder, bool isPrivate, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.CloudName)
            || string.IsNullOrWhiteSpace(options.ApiKey)
            || string.IsNullOrWhiteSpace(options.ApiSecret))
        {
            logger?.LogError("Cloudinary upload failed: CloudName, ApiKey, or ApiSecret is missing.");
            throw new DomainException("Image upload service credentials are not configured.", "UPLOAD_FAILED");
        }

        try
        {
            if (content.CanSeek)
                content.Seek(0, SeekOrigin.Begin);

            var preset = string.IsNullOrWhiteSpace(options.UploadPreset) ? null : options.UploadPreset.Trim();
            var result = await UploadOnceAsync(content, fileName, folder, preset, isPrivate, cancellationToken);

            // If a default preset ("modmove") has not been created in the Cloudinary console, retry signed upload without preset.
            if (result.Error is not null
                && preset is not null
                && content.CanSeek
                && result.Error.Message.Contains("preset", StringComparison.OrdinalIgnoreCase))
            {
                logger?.LogWarning(
                    "Cloudinary upload with preset '{Preset}' failed ({Error}); retrying signed upload without preset.",
                    preset, result.Error.Message);
                content.Seek(0, SeekOrigin.Begin);
                result = await UploadOnceAsync(content, fileName, folder, uploadPreset: null, isPrivate, cancellationToken);
            }

            if (result.Error is not null || result.SecureUrl is null)
            {
                var errorMessage = result.Error?.Message ?? "Cloudinary did not return a secure URL.";
                logger?.LogError("Cloudinary upload failed for {FileName} in {Folder}: {Error}", fileName, folder, errorMessage);
                throw MapUploadError(errorMessage);
            }

            return result.SecureUrl.ToString();
        }
        catch (DomainException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Unexpected Cloudinary upload failure for {FileName} in {Folder}.", fileName, folder);
            throw new DomainException("Image upload failed due to storage service error.", "UPLOAD_FAILED");
        }
    }

    public bool IsOwnedUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        string.Equals(uri.Host, "res.cloudinary.com", StringComparison.OrdinalIgnoreCase) &&
        uri.AbsolutePath.StartsWith($"/{options.CloudName}/", StringComparison.Ordinal);

    private Task<ImageUploadResult> UploadOnceAsync(
        Stream content, string fileName, string folder, string? uploadPreset, bool isPrivate, CancellationToken cancellationToken) =>
        _cloudinary.UploadAsync(new ImageUploadParams
        {
            File = new FileDescription(fileName, content),
            UploadPreset = uploadPreset,
            Folder = folder,
            AllowedFormats = ["jpg", "png"],
            Type = isPrivate ? CloudinaryUrls.AuthenticatedType : null,
        }, cancellationToken);

    private static DomainException MapUploadError(string message)
    {
        if (message.Contains("file size", StringComparison.OrdinalIgnoreCase)
            || message.Contains("too large", StringComparison.OrdinalIgnoreCase))
        {
            return new DomainException("The image exceeds the allowed file size.", "FILE_TOO_LARGE");
        }

        if (message.Contains("image file", StringComparison.OrdinalIgnoreCase)
            || message.Contains("format", StringComparison.OrdinalIgnoreCase)
            || message.Contains("unsupported", StringComparison.OrdinalIgnoreCase))
        {
            return new DomainException("Only JPG or PNG images are accepted.", "UNSUPPORTED_FILE_TYPE");
        }

        return new DomainException($"Image upload failed: {message}", "UPLOAD_FAILED");
    }
}
