using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Application.Common.Interfaces.Persistence;

public interface IOtpRepository : IRepository<OtpVerification>
{
    /// <summary>The most recent OTP issued to this email for this purpose (GB-25 limits apply to it).</summary>
    Task<OtpVerification?> GetLatestAsync(string email, OtpPurpose purpose, CancellationToken cancellationToken = default);
}
