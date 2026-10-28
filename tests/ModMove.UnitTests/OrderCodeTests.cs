using ModMove.Application.Features.Orders;

namespace ModMove.UnitTests;

public class OrderCodeTests
{
    [Fact]
    public void Code_uses_the_vietnam_date_9_5() =>
        Assert.Equal("MM20261028007", OrderCode.For(new DateTimeOffset(2026, 10, 27, 18, 30, 0, TimeSpan.Zero), 7)); // 28/10 01:30 in Vietnam

    [Fact]
    public void Vietnam_day_spans_17h_to_17h_utc() =>
        Assert.Equal(
            (new DateTimeOffset(2026, 10, 27, 17, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 28, 17, 0, 0, TimeSpan.Zero)),
            OrderCode.VietnamDay(new DateTimeOffset(2026, 10, 28, 3, 0, 0, TimeSpan.Zero)));
}
