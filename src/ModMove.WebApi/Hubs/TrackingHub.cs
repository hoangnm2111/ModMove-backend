using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ModMove.WebApi.Hubs;

/// <summary>
/// DECISIONS B12: hub /hubs/tracking. Server pushes go through <see cref="SignalRRealtimeNotifier"/>;
/// the client methods (JoinOrder, SendLocation) and the endpoint mapping come with live tracking.
/// </summary>
[Authorize]
public sealed class TrackingHub : Hub
{
    public const string DispatchersGroup = "dispatchers";

    public static string OrderGroup(long orderId) => $"order-{orderId}";
}
