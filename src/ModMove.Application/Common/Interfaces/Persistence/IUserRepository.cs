using ModMove.Domain.Entities;

namespace ModMove.Application.Common.Interfaces.Persistence;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>GB-01: true when another account already uses this email or phone number.</summary>
    Task<bool> IsEmailOrPhoneTakenAsync(string email, string phoneNumber, CancellationToken cancellationToken = default);

    /// <summary>GB-01 on profile update: true when an account other than <paramref name="userId"/> uses this phone.</summary>
    Task<bool> IsPhoneTakenByAnotherAsync(string phoneNumber, long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A device belongs to whoever signed in last: clear this FCM token from every other account, so one
    /// user's notifications never reach another user's phone. Runs immediately, outside SaveChanges.
    /// </summary>
    Task ReleaseFcmTokenAsync(string token, long keepForUserId, CancellationToken cancellationToken = default);
}
