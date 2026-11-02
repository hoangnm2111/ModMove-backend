using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Users.Commands.UpdateMyProfile;

/// <summary>
/// UC-CUS-06 / UC-ADM-02: edit name and phone. The email is the sign-in identity and is not editable here.
/// </summary>
public sealed record UpdateMyProfileCommand(string FullName, string PhoneNumber) : ICommand;
