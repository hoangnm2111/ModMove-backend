using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Features.Monitoring.Queries.ListOnlineDrivers;

namespace ModMove.WebApi.Controllers.Dispatcher;

public sealed class DriversController : ApiControllerBase
{
    /// <summary>
    /// UC-DIS-01 Step 4 (DECISIONS 6.4, B12): initial snapshot of all Online, Approved, Active drivers with a GPS update
    /// within the last 5 minutes (both idle and on an active order). Live updates stream via SignalR <c>LocationUpdated</c>.
    /// </summary>
    [HttpGet("online")]
    public Task<IReadOnlyList<OnlineDriverMapItemResponse>> ListOnline(CancellationToken cancellationToken) =>
        Sender.Send(new ListOnlineDriversQuery(), cancellationToken);
}

