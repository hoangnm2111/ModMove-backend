namespace ModMove.Application.Common.Interfaces;

/// <summary>
/// Reads the values the Administrator can change (DECISIONS C8). Read-only, safe for queries and commands.
/// A missing key is a setup error (the seed creates every key), not a user error.
/// </summary>
public interface IPlatformSettings
{
    Task<decimal> GetDecimalAsync(string key, CancellationToken cancellationToken);
}

/// <summary>The only platform_settings keys code may read. The seed uses the same constants.</summary>
public static class SettingKeys
{
    public const string CommissionRate = "COMMISSION_RATE";     // GB-14, e.g. 0.10
    public const string LoadingFee = "LOADING_FEE";             // VND, UC-GST-05
    public const string RequiredDeposit = "REQUIRED_DEPOSIT";  // VND, GB-16, GB-32
    public const string MatchingRadiusKm = "MATCHING_RADIUS_KM"; // DECISIONS B5
}
