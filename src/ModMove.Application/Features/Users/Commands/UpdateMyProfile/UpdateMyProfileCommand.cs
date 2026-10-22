using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Users.Commands.UpdateMyProfile;

/// <summary>
/// UC-CUS-06 / UC-ADM-02: edit name, phone and avatar. The email is the sign-in identity and is not editable here.
/// <see cref="AvatarUrl"/> is a URL returned by the upload endpoint (Cloudinary), or null to remove it.
/// </summary>
public sealed record UpdateMyProfileCommand(string FullName, string PhoneNumber, string? AvatarUrl) : ICommand;
