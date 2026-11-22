using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Features.Trips.Commands.UploadDriverImage;
using ModMove.WebApi.Services;

namespace ModMove.WebApi.Controllers.Driver;

/// <summary>Driver only (RoleConvention): api/driver/uploads. Upload pickup, POD, incident photos or onboarding documents (GB-06, NFR 2.3).</summary>
public sealed class UploadsController : ApiControllerBase
{
    /// <summary>
    /// Upload one photo (JPG/PNG, at most 5 MB, GB-06); returns its Cloudinary URL. <c>?purpose=Document</c> for the 7
    /// onboarding documents: stored private (NFR 2.3), the URL cannot be opened, Admin sees a 10-minute link instead.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(10_000_000)]
    public async Task<DriverUploadResponse> Upload(
        IFormFile file, [FromQuery] DriverUploadPurpose purpose, CancellationToken cancellationToken)
    {
        await using var content = await UploadStreamHelper.OpenSeekableStreamAsync(file, cancellationToken);
        return await Sender.Send(
            new UploadDriverImageCommand(content, file.FileName, file.ContentType ?? "", file.Length, purpose),
            cancellationToken);
    }
}

