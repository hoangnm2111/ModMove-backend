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
        var url = request.Purpose == DriverUploadPurpose.Document
            ? await storage.UploadPrivateImageAsync(request.Content, request.FileName, $"modmove/drivers/{user.Id}/documents", cancellationToken)
            : await storage.UploadImageAsync(request.Content, request.FileName, $"modmove/drivers/{user.Id}", cancellationToken);
        return new DriverUploadResponse(url);
    }
}

