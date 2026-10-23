using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Features.MasterData.Queries.GetPublicContent;

namespace ModMove.WebApi.Controllers.Public;

public sealed class PoliciesController : ApiControllerBase
{
    /// <summary>Warranty, refund, terms of use and privacy policies (UC-GST-04).</summary>
    [HttpGet]
    public Task<IReadOnlyList<ContentResponse>> GetAll(CancellationToken cancellationToken) =>
        Sender.Send(new GetPublicContentQuery(PublicContentCategories.Policy), cancellationToken);
}
