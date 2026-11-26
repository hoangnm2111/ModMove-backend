using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Trips.Commands.UploadDriverImage;

internal sealed class UploadDriverImageCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IFileStorage storage) : ICommandHandler<UploadDriverImageCommand, DriverUploadResponse>
{
    public async Task<DriverUploadResponse> Handle(UploadDriverImageCommand request, CancellationToken cancellationToken)
    {
        var user = await currentUser.LoadUserAsync(users, cancellationToken);
        var isDocument = request.Purpose == DriverUploadPurpose.Document;
        var folder = isDocument ? $"modmove/drivers/{user.Id}/documents" : $"modmove/drivers/{user.Id}";
        var url = await storage.UploadPrivateImageAsync(request.Content, request.FileName, folder, cancellationToken);
        // A-05: a trip photo comes back as a signed link to show the thumbnail; a document URL only identifies the photo.
        return new DriverUploadResponse(isDocument ? url : storage.CreateViewUrl(url));
    }
}

