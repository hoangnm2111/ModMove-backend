using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Features.Catalog.Queries.GetPublicConfig;

namespace ModMove.WebApi.Controllers.Public;

public sealed class ConfigController : ApiControllerBase
{
    /// <summary>
    /// Loading fee, driver deposit, matching radius and the fixed business limits, so the web and the apps never hard-code them.
    /// No sign-in needed. Short browser cache: the Administrator can change the money values.
    /// </summary>
    [HttpGet]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
    public Task<PublicConfigResponse> Get(CancellationToken cancellationToken) =>
        Sender.Send(new GetPublicConfigQuery(), cancellationToken);
}
