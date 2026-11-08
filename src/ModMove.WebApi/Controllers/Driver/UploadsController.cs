using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Features.Orders.Commands.UploadImage;

namespace ModMove.WebApi.Controllers.Driver;

/// <summary>Driver document upload before profile approval (UC-DRV-03). RoleConvention requires Driver.</summary>
public sealed class UploadsController : ApiControllerBase
{
    /// <summary>One JPG/PNG up to 5 MB; returns a URL for the five onboarding documents.</summary>
    [HttpPost]
    [RequestSizeLimit(6_000_000)]
    public async Task<UploadImageResponse> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        return await Sender.Send(new UploadImageCommand(content, file.FileName, file.ContentType, file.Length), cancellationToken);
    }
}
