using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Features.Pricing.Commands.CreateVehicleType;
using ModMove.Application.Features.Pricing.Commands.SetVehicleTypeStatus;
using ModMove.Application.Features.Pricing.Commands.UpdatePlatformSettings;
using ModMove.Application.Features.Pricing.Commands.UpdateVehicleTypeFares;
using ModMove.Application.Features.Pricing.Queries.GetPricingConfigurations;

namespace ModMove.WebApi.Controllers.Admin;

/// <summary>Admin only (RoleConvention): api/admin/pricing. Fare &amp; commission management, screen #35.</summary>
public sealed class PricingController : ApiControllerBase
{
    /// <summary>UC-ADM-10: all vehicle types (also disabled ones) and the four platform settings.</summary>
    [HttpGet]
    public Task<PricingConfigurationResponse> Get(CancellationToken cancellationToken) =>
        Sender.Send(new GetPricingConfigurationsQuery(), cancellationToken);

    /// <summary>UC-ADM-11/15: add a vehicle type (sizes in cm); it starts active. Name already used → 409.</summary>
    [HttpPost("vehicletypes")]
    public async Task<IActionResult> CreateVehicleType(CreateVehicleTypeCommand command, CancellationToken cancellationToken)
    {
        var id = await Sender.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new { id });
    }

    /// <summary>UC-ADM-11: unknown id → 404.</summary>
    [HttpPut("vehicletypes/{id:long}")]
    public async Task<IActionResult> UpdateVehicleTypeFares(long id, UpdateVehicleTypeFaresRequest body, CancellationToken cancellationToken)
    {
        await Sender.Send(new UpdateVehicleTypeFaresCommand(id, body.BaseFare, body.PricePerKm, body.MinimumFare), cancellationToken);
        return NoContent();
    }

    /// <summary>UC-ADM-11: enable or disable a vehicle type.</summary>
    [HttpPatch("vehicletypes/{id:long}/status")]
    public async Task<IActionResult> SetVehicleTypeStatus(long id, SetVehicleTypeStatusRequest body, CancellationToken cancellationToken)
    {
        await Sender.Send(new SetVehicleTypeStatusCommand(id, body.IsActive), cancellationToken);
        return NoContent();
    }

    /// <summary>UC-ADM-13 / DECISIONS C8: send only the fields to change. Changing the commission rate while orders are running → 422 COMMISSION_RATE_LOCKED.</summary>
    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings(UpdatePlatformSettingsCommand command, CancellationToken cancellationToken)
    {
        await Sender.Send(command, cancellationToken);
        return NoContent();
    }
}

/// <summary>Body of PUT vehicletypes/{id}; the id comes from the route, so it is not repeated here.</summary>
public sealed record UpdateVehicleTypeFaresRequest(decimal BaseFare, decimal PricePerKm, decimal MinimumFare);

/// <summary>Body of PATCH vehicletypes/{id}/status.</summary>
public sealed record SetVehicleTypeStatusRequest(bool IsActive);

