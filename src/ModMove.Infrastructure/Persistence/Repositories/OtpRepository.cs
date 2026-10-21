using Microsoft.EntityFrameworkCore;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Infrastructure.Persistence.Repositories;

public sealed class OtpRepository(ApplicationDbContext dbContext) : Repository<OtpVerification>(dbContext), IOtpRepository
{
    public Task<OtpVerification?> GetLatestAsync(string email, OtpPurpose purpose, CancellationToken cancellationToken = default) =>
        Set.Where(o => o.Email == email && o.Purpose == purpose)
            .OrderByDescending(o => o.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
