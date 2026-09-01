namespace TravelBlog.Web.Models;

public enum PostCardBreakpoint
{
    ExtraSmall,
    Small,
    Medium,
    Large
}

public static class PostCardSummaryLimits
{
    public const int ExtraSmall = 100;
    public const int Small = 140;
    public const int Medium = 180;
    public const int Large = 240;

    public const double CompactMultiplier = 0.6;

    public static int For(PostCardBreakpoint breakpoint, bool isCompactGallery) =>
        isCompactGallery
            ? (int)Math.Round(BaseLimit(breakpoint) * CompactMultiplier, MidpointRounding.AwayFromZero)
            : BaseLimit(breakpoint);

    private static int BaseLimit(PostCardBreakpoint breakpoint) =>
        breakpoint switch
        {
            PostCardBreakpoint.ExtraSmall => ExtraSmall,
            PostCardBreakpoint.Small => Small,
            PostCardBreakpoint.Medium => Medium,
            PostCardBreakpoint.Large => Large,
            _ => Large
        };
}
