using System.Globalization;

namespace ModMove.Application.Features.Orders;

/// <summary>Order codes MMyyyyMMddNNN by the Vietnam date (DECISIONS 9.5): NNN counts the day's orders from 001.</summary>
public static class OrderCode
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static string For(DateTimeOffset nowUtc, int sequenceInDay) =>
        "MM" + nowUtc.ToOffset(VietnamOffset).ToString("yyyyMMdd", CultureInfo.InvariantCulture) +
        sequenceInDay.ToString("D3", CultureInfo.InvariantCulture);

    /// <summary>The Vietnam calendar day containing <paramref name="nowUtc"/>, as a UTC range [from, to).</summary>
    public static (DateTimeOffset FromUtc, DateTimeOffset ToUtc) VietnamDay(DateTimeOffset nowUtc)
    {
        var local = nowUtc.ToOffset(VietnamOffset);
        var start = new DateTimeOffset(local.Year, local.Month, local.Day, 0, 0, 0, VietnamOffset).ToUniversalTime();
        return (start, start.AddDays(1));
    }
}
