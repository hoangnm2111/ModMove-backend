namespace ModMove.Infrastructure.Identity;

/// <summary>Bound from the "Jwt" configuration section. SigningKey comes from user secrets / appsettings.Development.json, never appsettings.json.</summary>
public sealed class JwtOptions
{
    public const int MinSigningKeyLength = 32;

    public string Issuer { get; init; } = "ModMove";
    public string Audience { get; init; } = "ModMove.Clients";
    public string SigningKey { get; init; } = "";
    public int AccessTokenMinutes { get; init; } = 15;
}
