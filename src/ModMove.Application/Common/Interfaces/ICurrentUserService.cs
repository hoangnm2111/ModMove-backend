namespace ModMove.Application.Common.Interfaces;

/// <summary>
/// The authenticated caller. Implemented in WebApi from the JWT claims,
/// so Application never touches HttpContext.
/// </summary>
public interface ICurrentUserService
{
    long? UserId { get; }
    bool IsInRole(string role);
}
