using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Features.MasterData.Queries.GetPublicContent;

namespace ModMove.WebApi.Controllers.Public;

/// <summary>Lists the client picks a code from; the Administrator keeps them in master data.</summary>
public sealed class LookupsController : ApiControllerBase
{
    /// <summary>Cargo types for <c>cargo.cargoType</c> when booking.</summary>
    [HttpGet("cargo-types")]
    public Task<IReadOnlyList<ContentResponse>> CargoTypes(CancellationToken cancellationToken) =>
        Sender.Send(new GetPublicContentQuery(PublicContentCategories.CargoType), cancellationToken);

    /// <summary>Reasons for <c>reasonCode</c> of a complaint; OTHER needs a written reason.</summary>
    [HttpGet("complaint-reasons")]
    public Task<IReadOnlyList<ContentResponse>> ComplaintReasons(CancellationToken cancellationToken) =>
        Sender.Send(new GetPublicContentQuery(PublicContentCategories.ComplaintReason), cancellationToken);

    /// <summary>Banks for <c>payoutAccount.bankCode</c> (code = BIN).</summary>
    [HttpGet("banks")]
    public Task<IReadOnlyList<ContentResponse>> Banks(CancellationToken cancellationToken) =>
        Sender.Send(new GetPublicContentQuery(PublicContentCategories.Bank), cancellationToken);

    /// <summary>Reasons for <c>reasonCode</c> of a cancellation; OTHER needs a written reason.</summary>
    [HttpGet("cancellation-reasons")]
    public Task<IReadOnlyList<ContentResponse>> CancellationReasons(CancellationToken cancellationToken) =>
        Sender.Send(new GetPublicContentQuery(PublicContentCategories.CancellationReason), cancellationToken);
}
