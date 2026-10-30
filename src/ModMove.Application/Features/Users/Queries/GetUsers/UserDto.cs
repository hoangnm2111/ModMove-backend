namespace ModMove.Application.Features.Users.Queries.GetUsers;

public sealed record UserDto
{
    public long Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string AccountStatus { get; init; } = string.Empty;
    public bool EmailVerified { get; init; }
    public DateTimeOffset CreatedAt { get; init; }

    public UserDto() { }

    public UserDto(
        long id,
        string fullName,
        string phoneNumber,
        string email,
        string role,
        string accountStatus,
        bool emailVerified,
        DateTimeOffset createdAt)
    {
        Id = id;
        FullName = fullName;
        PhoneNumber = phoneNumber;
        Email = email;
        Role = role;
        AccountStatus = accountStatus;
        EmailVerified = emailVerified;
        CreatedAt = createdAt;
    }
}

