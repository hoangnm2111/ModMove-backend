using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Common.Models;
using ModMove.Application.Features.Monitoring.Queries.ListHandledCases;

namespace ModMove.WebApi.Controllers.Dispatcher;

[Route("api/dispatcher/handled-cases")]
public sealed class HandledCasesController : ApiControllerBase
{
    /// <summary>
    /// UC-DIS-16: review the incidents and refund proposals handled by the signed-in Dispatcher
    /// (<c>type</c> = <c>Incident</c> | <c>Complaint</c> | <c>IncidentRefund</c> | <c>Refund</c>, or omitted for all).
    /// </summary>
    [HttpGet]
    public Task<PagedResult<HandledCaseItemResponse>> List(
        [FromQuery] ListHandledCasesQuery query, CancellationToken cancellationToken) =>
        Sender.Send(query, cancellationToken);
}

