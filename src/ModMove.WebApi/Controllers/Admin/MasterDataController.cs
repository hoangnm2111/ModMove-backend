using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Features.MasterData.Commands.CreateMasterDataItem;
using ModMove.Application.Features.MasterData.Commands.SetMasterDataStatus;
using ModMove.Application.Features.MasterData.Commands.UpdateMasterDataItem;
using ModMove.Application.Features.MasterData.Queries.GetMasterDataList;

namespace ModMove.WebApi.Controllers.Admin;

/// <summary>Admin only: api/admin/masterdata. Master data management, screen #36.</summary>
public sealed class MasterDataController : ApiControllerBase
{
    /// <summary>UC-ADM-14: list master data items, optionally filtered by category.</summary>
    [HttpGet]
    public Task<IReadOnlyList<MasterDataItemDto>> GetList([FromQuery] string? category, CancellationToken cancellationToken) =>
        Sender.Send(new GetMasterDataListQuery(category), cancellationToken);

    /// <summary>UC-ADM-15: create a new master data lookup entry.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(CreateMasterDataItemRequest body, CancellationToken cancellationToken)
    {
        var id = await Sender.Send(new CreateMasterDataItemCommand(body.Category, body.Code, body.Name, body.Description), cancellationToken);
        return CreatedAtAction(nameof(GetList), new { category = body.Category }, new { id });
    }

    /// <summary>UC-ADM-16 / DECISIONS B13: update name and description (e.g. policy texts).</summary>
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, UpdateMasterDataItemRequest body, CancellationToken cancellationToken)
    {
        await Sender.Send(new UpdateMasterDataItemCommand(id, body.Name, body.Description), cancellationToken);
        return NoContent();
    }

    /// <summary>UC-ADM-16: enable or disable a master data item.</summary>
    [HttpPatch("{id:long}/status")]
    public async Task<IActionResult> SetStatus(long id, SetMasterDataStatusRequest body, CancellationToken cancellationToken)
    {
        await Sender.Send(new SetMasterDataStatusCommand(id, body.IsActive), cancellationToken);
        return NoContent();
    }
}

public sealed record CreateMasterDataItemRequest(string Category, string Code, string Name, string? Description);
public sealed record UpdateMasterDataItemRequest(string Name, string? Description);
public sealed record SetMasterDataStatusRequest(bool IsActive);
