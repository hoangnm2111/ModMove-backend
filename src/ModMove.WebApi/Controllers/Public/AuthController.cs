using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Features.Auth.Commands.Login;

namespace ModMove.WebApi.Controllers.Public;

public sealed class AuthController : ApiControllerBase
{
    /// <summary>Sign in with email and password. One endpoint for every role; the token carries the role.</summary>
    [HttpPost("login")]
    public Task<LoginResponse> Login(LoginCommand command, CancellationToken cancellationToken) =>
        Sender.Send(command, cancellationToken);
}
