using ModMove.Domain.Common;
using ModMove.Domain.Constants;
using ModMove.Domain.Enums;

namespace ModMove.Domain.Entities;

/// <summary>Every account in the system (Customer, Driver, Dispatcher, Administrator).</summary>
public sealed class User : BaseAuditableEntity, IAggregateRoot
{
    private User() { }

    public string FullName { get; private set; } = null!;
    public string PhoneNumber { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public AccountStatus AccountStatus { get; private set; } = AccountStatus.Active;
    public bool EmailVerified { get; private set; }
    public int FailedLoginCount { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }
    public string? FcmToken { get; private set; }

    public static User Create(string fullName, string phoneNumber, string email, string passwordHash, UserRole role, bool emailVerified = false) =>
        new()
        {
            FullName = fullName,
            PhoneNumber = phoneNumber,
            Email = email,
            PasswordHash = passwordHash,
            Role = role,
            EmailVerified = emailVerified,
        };

    public bool IsLocked(DateTimeOffset now) => LockedUntil is not null && LockedUntil > now;

    /// <summary>GB-02: lock for 10 minutes after 5 consecutive failed sign-ins.</summary>
    public void RegisterFailedLogin(DateTimeOffset now)
    {
        FailedLoginCount++;
        if (FailedLoginCount >= BusinessRules.MaxFailedLogins)
        {
            LockedUntil = now + BusinessRules.LoginLockDuration;
            FailedLoginCount = 0;
        }
    }

    public void RegisterSuccessfulLogin()
    {
        FailedLoginCount = 0;
        LockedUntil = null;
    }

    public void VerifyEmail() => EmailVerified = true;

    /// <summary>DECISIONS A-02: an unverified, active account of the same role is registered over instead of being a conflict.</summary>
    public bool CanRegisterAgainAs(UserRole role) =>
        !EmailVerified && AccountStatus == AccountStatus.Active && Role == role;

    public void RegisterAgain(string fullName, string phoneNumber, string passwordHash)
    {
        FullName = fullName;
        PhoneNumber = phoneNumber;
        PasswordHash = passwordHash;
    }

    public void ChangeStatus(AccountStatus status) => AccountStatus = status;

    public void ChangePassword(string passwordHash) => PasswordHash = passwordHash;

    public void UpdateProfile(string fullName, string phoneNumber)
    {
        FullName = fullName;
        PhoneNumber = phoneNumber;
    }

    public void SetFcmToken(string? token) => FcmToken = token;
}
