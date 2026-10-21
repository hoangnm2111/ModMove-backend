using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Features.Catalog.Queries.GetVehicleTypes;

namespace ModMove.WebApi.Controllers.Public;

public sealed class VehicleTypesController : ApiControllerBase
{
    /// <summary>Active vehicle types with capacity and fares (UC-GST-02). No sign-in needed.</summary>
    [HttpGet]
    public Task<IReadOnlyList<VehicleTypeResponse>> GetAll(CancellationToken cancellationToken) =>
        Sender.Send(new GetVehicleTypesQuery(), cancellationToken);
}
