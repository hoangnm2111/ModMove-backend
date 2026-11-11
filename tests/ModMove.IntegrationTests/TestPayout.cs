using ModMove.Application.Features.Refunds;

namespace ModMove.IntegrationTests;

/// <summary>R-01: a payout account the BANK list in <see cref="InMemoryMasterData"/> accepts.</summary>
internal static class TestPayout
{
    public static PayoutAccountInput Valid { get; } = new(false, "970436", "0123456789", "NGUYEN VAN A");
}
