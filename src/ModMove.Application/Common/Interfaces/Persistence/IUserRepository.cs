using ModMove.Domain.Entities;

namespace ModMove.Application.Common.Interfaces.Persistence;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>GB-01: true when another account already uses this email or phone number.</summary>
    Task<bool> IsEmailOrPhoneTakenAsync(string email, string phoneNumber, CancellationToken cancellationToken = default);
}
