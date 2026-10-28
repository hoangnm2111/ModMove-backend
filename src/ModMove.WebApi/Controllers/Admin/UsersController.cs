using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Common.Models;
using ModMove.Application.Features.Users.Commands.CreateDispatcher;
using ModMove.Application.Features.Users.Commands.UpdateUserStatus;
using ModMove.Application.Features.Users.Queries.GetUserDetail;
using ModMove.Application.Features.Users.Queries.GetUsers;
using ModMove.Domain.Enums;

namespace ModMove.WebApi.Controllers.Admin;

/// <summary>Admin only (RoleConvention): api/admin/users. Account management (UC-ADM-04..07).</summary>
public sealed class UsersController : ApiControllerBase
{
    /// <summary>UC-ADM-04: search, filter, and browse user accounts with pagination.</summary>
    [HttpGet]
    public Task<PagedResult<UserDto>> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] UserRole? role = null,
        [FromQuery] AccountStatus? status = null,
        CancellationToken cancellationToken = default) =>
        Sender.Send(new GetUsersQuery(page, pageSize, search, role, status), cancellationToken);

    /// <summary>UC-ADM-05: view detailed user profile, account status, and order summary.</summary>
    [HttpGet("{id:long}")]
    public Task<UserDetailResponse> GetDetail(long id, CancellationToken cancellationToken) =>
        Sender.Send(new GetUserDetailQuery(id), cancellationToken);

    /// <summary>
    /// UC-ADM-06 (DECISIONS B8, 8.2): update user account status (Active, Inactive, Locked) with a reason.
    /// Setting Inactive or Locked revokes all active sessions. Changing own account → 422 CANNOT_CHANGE_OWN_STATUS.
    /// </summary>
    [HttpPatch("{id:long}/status")]
    public async Task<IActionResult> UpdateStatus(long id, UpdateUserStatusRequest body, CancellationToken cancellationToken)
    {
        await Sender.Send(new UpdateUserStatusCommand(id, body.Status, body.Reason), cancellationToken);
        return NoContent();
    }

    /// <summary>UC-ADM-07 (DECISIONS B7): create an internal Dispatcher account → 201 { id }.</summary>
    [HttpPost("dispatchers")]
    public async Task<IActionResult> CreateDispatcher(CreateDispatcherCommand command, CancellationToken cancellationToken)
    {
        var id = await Sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetDetail), new { id }, new { id });
    }
}

public sealed record UpdateUserStatusRequest(AccountStatus Status, string Reason);

