using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Trips.Commands.UploadDriverImage;

/// <summary>
/// One photo uploaded by a driver (GB-06: JPG/PNG, at most 5 MB) for pickup evidence, proof of delivery, or incident reports.
/// </summary>
public sealed record UploadDriverImageCommand(
    Stream Content, string FileName, string ContentType, long Length) : ICommand<DriverUploadResponse>;

public sealed record DriverUploadResponse(string Url);

