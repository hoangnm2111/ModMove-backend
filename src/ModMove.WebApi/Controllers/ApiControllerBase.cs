using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ModMove.WebApi.Controllers;

/// <summary>
/// Controllers stay thin: map HTTP -> Command/Query, send it, map result -> HTTP.
/// No business logic and no direct Infrastructure access here.
/// </summary>
[ApiController]
[Route(RoleConvention.PlaceholderRoute)] // [ApiController] demands a route before conventions run; RoleConvention rewrites it
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _sender;

    protected ISender Sender => _sender ??= HttpContext.RequestServices.GetRequiredService<ISender>();
}
