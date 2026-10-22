using System.Globalization;
using Dapper;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;

namespace ModMove.Infrastructure.Persistence;

// shortcut: reads the DB on every call; add a short in-memory cache if pricing or matching gets hot.
public sealed class PlatformSettings(ISqlConnectionFactory connectionFactory) : IPlatformSettings
{
    public async Task<decimal> GetDecimalAsync(string key, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var raw = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT setting_value FROM platform_settings WHERE setting_key = @Key",
            new { Key = key },
            cancellationToken: cancellationToken));

        return Parse(key, raw);
    }

    // Values are stored as text ("0.10", "100000"); always parse with the invariant culture, never the server's.
    public static decimal Parse(string key, string? raw) =>
        raw is null
            ? throw new InvalidOperationException($"Platform setting '{key}' is missing. Run the seed.")
            : decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new InvalidOperationException($"Platform setting '{key}' is not a number: '{raw}'.");
}
