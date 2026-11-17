using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Features.Orders.Commands.UploadImage;
using ModMove.WebApi.Services;

namespace ModMove.WebApi.Controllers.Customer;

public sealed class UploadsController : ApiControllerBase
{
    /// <summary>Upload one cargo photo (JPG/PNG, at most 5 MB, GB-06); returns its URL for the order form.</summary>
    [HttpPost]
    [RequestSizeLimit(10_000_000)]
    public async Task<UploadImageResponse> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        await using var content = await UploadStreamHelper.OpenSeekableStreamAsync(file, cancellationToken);
        return await Sender.Send(new UploadImageCommand(content, file.FileName, file.ContentType ?? "", file.Length), cancellationToken);
    }
}
