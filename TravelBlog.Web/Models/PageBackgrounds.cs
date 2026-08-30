namespace TravelBlog.Web.Models;

/// <summary>
/// Optional decorative backgrounds for individual pages.
/// Set in a view: ViewData[PageBackgrounds.ViewDataKey] = PageBackgrounds.Notebook;
/// </summary>
public static class PageBackgrounds
{
    public const string ViewDataKey = "PageBackground";

    public const string Notebook = "notebook";

    public static string? GetBodyClass(object? background) =>
        background is not string value || string.IsNullOrWhiteSpace(value)
            ? null
            : $"page-bg page-bg--{value.Trim().ToLowerInvariant()}";
}
