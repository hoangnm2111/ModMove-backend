using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Orders.Commands.UploadImage;

/// <summary>
/// One cargo photo from the customer (GB-06: JPG/PNG, at most 5 MB). The returned URL is what the order form sends back.
/// </summary>
public sealed record UploadImageCommand(Stream Content, string FileName, string ContentType, long Length) : ICommand<UploadImageResponse>;

public sealed record UploadImageResponse(string Url);
