using System.Globalization;
using Microsoft.AspNetCore.SignalR;
using ModMove.Application.Common.Interfaces;

namespace ModMove.WebApi.Hubs;

/// <summary>
/// The SignalR adapter lives in WebApi, next to the hub it needs: Infrastructure may not reference ASP.NET Core.
/// Clients.User matches the NameIdentifier claim of the JWT (B17: no custom user-{id} group).
/// </summary>
public sealed class SignalRRealtimeNotifier(IHubContext<TrackingHub> hub) : IRealtimeNotifier
{
    public Task SendToOrderAsync(long orderId, string eventName, object payload, CancellationToken cancellationToken) =>
        hub.Clients.Group(TrackingHub.OrderGroup(orderId)).SendAsync(eventName, payload, cancellationToken);

    public Task SendToUserAsync(long userId, string eventName, object payload, CancellationToken cancellationToken) =>
        hub.Clients.User(userId.ToString(CultureInfo.InvariantCulture)).SendAsync(eventName, payload, cancellationToken);

    public Task SendToDispatchersAsync(string eventName, object payload, CancellationToken cancellationToken) =>
        hub.Clients.Group(TrackingHub.DispatchersGroup).SendAsync(eventName, payload, cancellationToken);
}
