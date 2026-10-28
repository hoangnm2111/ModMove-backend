using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Features.Orders.Commands.CreateOrder;

namespace ModMove.WebApi.Controllers.Customer;

public sealed class OrdersController : ApiControllerBase
{
    /// <summary>Book a cash order (UC-GST-17). Price is computed again server-side, the same way as the fare estimate.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
