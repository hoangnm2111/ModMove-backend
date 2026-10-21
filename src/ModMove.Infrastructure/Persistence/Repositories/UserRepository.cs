using Microsoft.EntityFrameworkCore;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;

namespace ModMove.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(ApplicationDbContext dbContext) : Repository<User>(dbContext), IUserRepository
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<bool> IsEmailOrPhoneTakenAsync(string email, string phoneNumber, CancellationToken cancellationToken = default) =>
        Set.AnyAsync(u => u.Email == email || u.PhoneNumber == phoneNumber, cancellationToken);
}
