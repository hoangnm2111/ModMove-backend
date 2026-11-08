using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Orders.Commands.UploadImage;

internal sealed class UploadImageCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IFileStorage storage) : ICommandHandler<UploadImageCommand, UploadImageResponse>
{
    public async Task<UploadImageResponse> Handle(UploadImageCommand request, CancellationToken cancellationToken)
    {
        var user = await currentUser.LoadUserAsync(users, cancellationToken);

        var folder = user.Role == UserRole.Driver ? "drivers" : "customers";
        var url = await storage.UploadImageAsync(request.Content, request.FileName, $"modmove/{folder}/{user.Id}", cancellationToken);
        return new UploadImageResponse(url);
    }
}
