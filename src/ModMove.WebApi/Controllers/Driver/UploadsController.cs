using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Features.Trips.Commands.UploadDriverImage;
using ModMove.WebApi.Services;

namespace ModMove.WebApi.Controllers.Driver;

/// <summary>Driver only (RoleConvention): api/driver/uploads. Upload pickup, POD, or incident photos (GB-06).</summary>
public sealed class UploadsController : ApiControllerBase
{
    /// <summary>Upload one photo (JPG/PNG, at most 5 MB, GB-06); returns its Cloudinary URL.</summary>
    [HttpPost]
    [RequestSizeLimit(10_000_000)]
    public async Task<DriverUploadResponse> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        await using var content = await UploadStreamHelper.OpenSeekableStreamAsync(file, cancellationToken);
        return await Sender.Send(
            new UploadDriverImageCommand(content, file.FileName, file.ContentType ?? "", file.Length),
            cancellationToken);
    }
}

