using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Trips.Commands.UploadDriverImage;

/// <summary>
/// One photo uploaded by a driver (GB-06: JPG/PNG, at most 5 MB). <see cref="DriverUploadPurpose.Trip"/>: pickup evidence,
/// proof of delivery or incident photos, which the customer sees. <see cref="DriverUploadPurpose.Document"/>: one of the
/// 7 onboarding documents (GB-04), stored private (NFR 2.3); the URL returned only identifies it.
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

