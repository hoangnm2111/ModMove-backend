using System.Security.Claims;
using ModMove.Application.Common.Interfaces;

namespace ModMove.WebApi.Services;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public long? UserId =>
        long.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;
}
