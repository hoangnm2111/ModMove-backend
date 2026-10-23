using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Users.Queries.GetMyProfile;

/// <summary>UC-CUS-05 / UC-ADM-02: the signed-in user's own account (any role).</summary>
public sealed record GetMyProfileQuery : IQuery<ProfileResponse>;

public sealed record ProfileResponse(
    long Id,
    string FullName,
    string PhoneNumber,
    string Email,
    string Role,
    string? AvatarUrl,
    bool EmailVerified);
