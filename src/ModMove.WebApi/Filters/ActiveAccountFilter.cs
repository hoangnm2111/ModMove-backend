using MediatR;
using Microsoft.AspNetCore.Mvc.Filters;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Tracking.Queries.IsActiveAccount;

namespace ModMove.WebApi.Filters;

/// <summary>
/// DECISIONS B8: a token outlives a lock or a deactivation, so a Dispatcher or Administrator request is refused with
/// 403 ACCOUNT_INACTIVE unless the account is still Active. Customer and Driver handlers check this themselves.
/// </summary>
public sealed class ActiveAccountFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        // Authorization already ran: the role folders accept only signed-in users, so the id is there.
        var userId = services.GetRequiredService<ICurrentUserService>().UserId ?? throw new UnauthorizedException("Sign in again.");

        if (!await services.GetRequiredService<ISender>().Send(new IsActiveAccountQuery(userId), context.HttpContext.RequestAborted))
            throw new ForbiddenAccessException("The account is not active.", "ACCOUNT_INACTIVE"); // same answer as LoadUserAsync

        await next();
    }
}
