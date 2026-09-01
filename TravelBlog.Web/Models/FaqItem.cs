namespace TravelBlog.Web.Models;

public sealed class FaqItem
{
    public required string Question { get; init; }

    public required string Answer { get; init; }
}

public sealed class FaqViewModel
{
    public required IReadOnlyList<FaqItem> Items { get; init; }
}
