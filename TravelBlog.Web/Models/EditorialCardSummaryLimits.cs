namespace TravelBlog.Web.Models;

public static class EditorialCardSummaryLimits
{
    // 280×390 card: ~4 summary lines when the title is a single line.
    public const int MaxSummaryLines = 4;
    public const int CharsPerLine = 32;
    public const int CharsPerTitleLine = 32;
    public const int MinLimit = 64;

    public static int ForTitle(string? title)
    {
        var titleLines = string.IsNullOrWhiteSpace(title)
            ? 1
            : (title.Length + CharsPerTitleLine - 1) / CharsPerTitleLine;

        var summaryLines = Math.Max(2, MaxSummaryLines - Math.Max(0, titleLines - 1));
        return Math.Max(MinLimit, summaryLines * CharsPerLine - 1);
    }
}
