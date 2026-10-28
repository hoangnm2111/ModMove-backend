using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Users.Queries.GetUsers;

/// <summary>UC-ADM-04: paginated user account list with optional role, status, and keyword filters.</summary>
public sealed record GetUsersQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    UserRole? Role = null,
    AccountStatus? Status = null) : IQuery<PagedResult<UserDto>>;

