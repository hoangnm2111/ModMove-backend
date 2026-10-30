using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Users.Queries.GetUserDetail;

/// <summary>UC-ADM-05: view detailed user profile, account status, driver info, and order summary.</summary>
public sealed record GetUserDetailQuery(long Id) : IQuery<UserDetailResponse>;

public sealed record UserDetailResponse
{
    public long Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string AccountStatus { get; init; } = string.Empty;
    public bool EmailVerified { get; init; }
    public string? AvatarUrl { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public string? DriverApprovalStatus { get; init; }
    public string? DriverOnlineStatus { get; init; }
    public string? LicensePlate { get; init; }
    public int TotalOrders { get; init; }
    public int CompletedOrders { get; init; }

    public UserDetailResponse() { }

    public UserDetailResponse(
        long id,
        string fullName,
        string phoneNumber,
        string email,
        string role,
        string accountStatus,
        bool emailVerified,
        string? avatarUrl,
        DateTimeOffset createdAt,
        DateTimeOffset? updatedAt,
        string? driverApprovalStatus,
        string? driverOnlineStatus,
        string? licensePlate,
        int totalOrders,
        int completedOrders)
    {
        Id = id;
        FullName = fullName;
        PhoneNumber = phoneNumber;
        Email = email;
        Role = role;
        AccountStatus = accountStatus;
        EmailVerified = emailVerified;
        AvatarUrl = avatarUrl;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        DriverApprovalStatus = driverApprovalStatus;
        DriverOnlineStatus = driverOnlineStatus;
        LicensePlate = licensePlate;
        TotalOrders = totalOrders;
        CompletedOrders = completedOrders;
    }
}

