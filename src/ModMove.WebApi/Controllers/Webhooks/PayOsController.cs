using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Features.Payments.Commands.ProcessPayOsWebhook;

namespace ModMove.WebApi.Controllers.Webhooks;

/// <summary>
/// Anonymous webhook endpoint (<c>POST api/webhooks/payos</c>) called by PayOS when a transfer arrives.
/// Authenticity is verified inside the handler via HMAC-SHA256 (<c>IPaymentGateway.VerifyWebhookSignature</c>).
/// </summary>
public sealed class PayOsController : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Handle([FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        await Sender.Send(new ProcessPayOsWebhookCommand(payload.GetRawText()), cancellationToken);
        return Ok(new { success = true });
    }
}

