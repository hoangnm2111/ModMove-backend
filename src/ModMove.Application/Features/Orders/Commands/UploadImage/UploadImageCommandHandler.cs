using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Orders.Commands.UploadImage;

internal sealed class UploadImageCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IFileStorage storage) : ICommandHandler<UploadImageCommand, UploadImageResponse>
{
    public async Task<UploadImageResponse> Handle(UploadImageCommand request, CancellationToken cancellationToken)
    {
        var user = await currentUser.LoadUserAsync(users, cancellationToken);

        // SRS IV.2.3: order photos are private; the signed link lets the client show the thumbnail (A-05).
        var url = await storage.UploadPrivateImageAsync(request.Content, request.FileName, $"modmove/customers/{user.Id}", cancellationToken);
        return new UploadImageResponse(storage.CreateViewUrl(url));
    }
}
