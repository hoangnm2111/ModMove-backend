using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Trips.Commands.UploadDriverImage;

/// <summary>
/// One photo uploaded by a driver (GB-06: JPG/PNG, at most 5 MB). <see cref="DriverUploadPurpose.Trip"/>: pickup evidence,
/// proof of delivery or incident photos, which the customer sees; the URL returned is a short-lived signed link.
/// <see cref="DriverUploadPurpose.Document"/>: one of the 7 onboarding documents (GB-04); the URL returned only
/// identifies it. Both are stored private (NFR 2.3, A-05).
/// </summary>
public sealed record UploadDriverImageCommand(
    Stream Content, string FileName, string ContentType, long Length,
    DriverUploadPurpose Purpose = DriverUploadPurpose.Trip) : ICommand<DriverUploadResponse>;

public enum DriverUploadPurpose
{
    Trip = 0,
    Document = 1,
}

public sealed record DriverUploadResponse(string Url);

