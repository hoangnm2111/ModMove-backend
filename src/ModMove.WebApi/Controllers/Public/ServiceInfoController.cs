using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Features.MasterData.Queries.GetPublicContent;

namespace ModMove.WebApi.Controllers.Public;

public sealed class ServiceInfoController : ApiControllerBase
{
    /// <summary>About the app, coverage area and benefits (UC-GST-03).</summary>
    [HttpGet]
    public Task<IReadOnlyList<ContentResponse>> GetAll(CancellationToken cancellationToken) =>
        Sender.Send(new GetPublicContentQuery(PublicContentCategories.ServiceInfo), cancellationToken);
}
