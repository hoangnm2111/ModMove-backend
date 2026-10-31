using System.Globalization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Features.Tracking.Commands.UpdateDriverLocation;
using ModMove.Application.Features.Tracking.Queries.CanFollowOrder;
using ModMove.Application.Features.Tracking.Queries.IsActiveAccount;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.WebApi.Hubs;

/// <summary>
/// DECISIONS B12: hub /hubs/tracking, JWT in the access_token query string. Like a controller it only sends to MediatR;
/// the caller's id is taken from the token (Context.UserIdentifier), never from a hub argument. Errors reach the client
/// as a HubException whose message starts with the stable code (ORDER_NOT_YOURS, ACCOUNT_INACTIVE, ...).
/// Server pushes go through <see cref="SignalRRealtimeNotifier"/>.
/// </summary>
[Authorize]
public sealed class TrackingHub(ISender sender) : Hub
{
    public const string DispatchersGroup = "dispatchers";

    public static string OrderGroup(long orderId) => $"order-{orderId}";

    private long UserId => long.Parse(Context.UserIdentifier!, CultureInfo.InvariantCulture);

    public override async Task OnConnectedAsync()
    {
        // A token outlives a lock or a deletion (B8): such an account is dropped at once.
        if (!await sender.Send(new IsActiveAccountQuery(UserId), Context.ConnectionAborted))
        {
            Context.Abort();
            return;
        }

        // Dispatchers see every Online driver on the map without asking (B12).
        if (Context.User?.IsInRole(nameof(UserRole.Dispatcher)) == true)
            await Groups.AddToGroupAsync(Context.ConnectionId, DispatchersGroup, Context.ConnectionAborted);

        await base.OnConnectedAsync();
    }

    /// <summary>Customer opens the tracking screen of their order; call again after a reconnect.</summary>
    [Authorize(Roles = nameof(UserRole.Customer))]
    public Task JoinOrder(long orderId) => WithErrorCodes(async () =>
    {
        if (!await sender.Send(new CanFollowOrderQuery(UserId, orderId), Context.ConnectionAborted))
            throw new DomainException("You can follow only your own order.", "ORDER_NOT_YOURS");

        await Groups.AddToGroupAsync(Context.ConnectionId, OrderGroup(orderId), Context.ConnectionAborted);
    });

    /// <summary>Driver app sends its GPS every few seconds while Online.</summary>
    [Authorize(Roles = nameof(UserRole.Driver))]
    public Task SendLocation(decimal latitude, decimal longitude) => WithErrorCodes(() =>
        sender.Send(new UpdateDriverLocationCommand(UserId, latitude, longitude), Context.ConnectionAborted));

    private static async Task WithErrorCodes(Func<Task> call)
    {
        try
        {
            await call();
        }
        catch (Exception ex) when (CodeOf(ex) is { } code)
        {
            throw new HubException($"{code}: {ex.Message}");
        }
    }

    // The same codes the REST API puts in ProblemDetails.code; anything else stays a hidden server error.
    private static string? CodeOf(Exception ex) => ex switch
    {
        ValidationException => "VALIDATION",
        UnauthorizedException => "UNAUTHORIZED",
        NotFoundException => "NOT_FOUND",
        ConflictException => "CONFLICT",
        ForbiddenAccessException forbidden => forbidden.Code ?? "FORBIDDEN",
        DomainException domain => domain.Code ?? "BUSINESS_RULE",
        _ => null,
    };
}
