using TravelBlog.Web.Models;

namespace TravelBlog.Web.Tests;

public sealed class PostCardSummaryLimitsTests
{
    [Theory]
    [InlineData(PostCardBreakpoint.ExtraSmall, false, 100)]
    [InlineData(PostCardBreakpoint.ExtraSmall, true, 60)]
    [InlineData(PostCardBreakpoint.Small, false, 140)]
    [InlineData(PostCardBreakpoint.Small, true, 84)]
    [InlineData(PostCardBreakpoint.Medium, false, 180)]
    [InlineData(PostCardBreakpoint.Medium, true, 108)]
    [InlineData(PostCardBreakpoint.Large, false, 240)]
    [InlineData(PostCardBreakpoint.Large, true, 144)]
    public void For_AppliesCompactMultiplier(
        PostCardBreakpoint breakpoint,
        bool isCompactGallery,
        int expected)
    {
        Assert.Equal(
            expected,
            PostCardSummaryLimits.For(breakpoint, isCompactGallery));
    }
}
