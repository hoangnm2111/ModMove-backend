using System.Globalization;
using ModMove.Infrastructure.Persistence;

namespace ModMove.IntegrationTests;

public class PlatformSettingsTests
{
    [Theory]
    [InlineData("0.10", 0.10)]
    [InlineData("100000", 100000)]
    [InlineData("5", 5)]
    public void Stored_text_is_parsed_with_the_invariant_culture(string raw, double expected)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("vi-VN"); // vi-VN uses ',' for decimals: "0.10" must still be 0.10
        try
        {
            Assert.Equal((decimal)expected, PlatformSettings.Parse("KEY", raw));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("ten percent")]
    public void Missing_or_non_numeric_setting_is_a_setup_error(string? raw)
    {
        var error = Assert.Throws<InvalidOperationException>(() => PlatformSettings.Parse("COMMISSION_RATE", raw));

        Assert.Contains("COMMISSION_RATE", error.Message);
    }
}
