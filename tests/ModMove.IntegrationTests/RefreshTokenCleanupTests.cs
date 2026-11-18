using ModMove.Application.Features.Auth.Commands.PurgeExpiredRefreshTokens;
using ModMove.Domain.Constants;
using ModMove.Domain.Entities;

namespace ModMove.IntegrationTests;

/// <summary>JOB-AUTH-01: expired refresh tokens are deleted; live rotated ones stay so token reuse is still caught (9.1).</summary>
public class PurgeExpiredRefreshTokensTests
{
    [Fact]
    public async Task Expired_tokens_are_deleted_and_live_rotated_or_revoked_ones_kept()
    {
        var app = new TestApp();
        var now = TestApp.Start;
        app.RefreshTokens.Add(RefreshToken.Issue(1, "expired", now - BusinessRules.RefreshTokenLifetime));
        var rotated = RefreshToken.Issue(1, "rotated", now - BusinessRules.RefreshTokenLifetime + TimeSpan.FromSeconds(1));
        rotated.Rotate(now);
        var revoked = RefreshToken.Issue(1, "revoked", now);
        revoked.Revoke(now);
        app.RefreshTokens.Add(rotated);
        app.RefreshTokens.Add(revoked);

        var deleted = await app.Sender.Send(new PurgeExpiredRefreshTokensCommand());

        Assert.Equal(1, deleted);
        Assert.Equal(["rotated", "revoked"], app.RefreshTokens.Items.Select(t => t.TokenHash));
    }
}
