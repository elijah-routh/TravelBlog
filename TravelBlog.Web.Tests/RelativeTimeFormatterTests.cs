using TravelBlog.Web.Services;

namespace TravelBlog.Web.Tests;

public sealed class RelativeTimeFormatterTests
{
    private static readonly DateTime Now = new(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(0, "0 mins ago")]
    [InlineData(1, "1 min ago")]
    [InlineData(59, "59 mins ago")]
    [InlineData(60, "1 hour ago")]
    [InlineData(120, "2 hours ago")]
    [InlineData(1440, "1 day ago")]
    [InlineData(10080, "1 week ago")]
    [InlineData(20160, "2 weeks ago")]
    [InlineData(64800, "1 month ago")]
    [InlineData(576000, "1 year ago")]
    public void FormatAgo_UsesExpectedUnits(int minutesAgo, string expected)
    {
        var timestamp = Now.AddMinutes(-minutesAgo);
        Assert.Equal(
            expected,
            RelativeTimeFormatter.FormatAgo(timestamp, Now));
    }
}
