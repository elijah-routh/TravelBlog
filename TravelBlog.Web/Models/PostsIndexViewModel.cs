namespace TravelBlog.Web.Models;

public sealed class PostsIndexViewModel
{
    public IReadOnlyList<Post> Posts { get; init; } = [];

    public string Scope { get; init; } = PostListScope.All;

    public string Sort { get; init; } = PostSortOrder.Newest;

    public string Status { get; init; } = PostPublishFilter.Published;

    public bool ShowUnpublished { get; init; }

    public bool ShowHidden { get; init; }

    public bool IsAdmin { get; init; }

    public bool IsCompactGallery { get; init; }

    public bool IsAuthenticated { get; init; }

    public bool CanWrite { get; init; }

    public bool CanLike { get; init; }

    public IReadOnlyDictionary<int, int> LikeCounts { get; init; } =
        new Dictionary<int, int>();

    public IReadOnlyDictionary<int, int> ViewCounts { get; init; } =
        new Dictionary<int, int>();

    public IReadOnlySet<int> LikedPostIds { get; init; } = new HashSet<int>();
}

public static class PostListScope
{
    public const string All = "all";
    public const string Mine = "mine";

    public static string Normalize(string? scope) =>
        string.Equals(scope, Mine, StringComparison.OrdinalIgnoreCase)
            ? Mine
            : All;
}

public static class PostSortOrder
{
    public const string Newest = "newest";
    public const string Oldest = "oldest";
    public const string MostLiked = "liked";

    public static string Normalize(string? sort) =>
        sort?.ToLowerInvariant() switch
        {
            Oldest => Oldest,
            MostLiked => MostLiked,
            _ => Newest
        };
}

public static class PostPublishFilter
{
    public const string Published = "published";
    public const string Unpublished = "unpublished";
    public const string Both = "both";

    public static string Normalize(string? status) =>
        status?.ToLowerInvariant() switch
        {
            Unpublished => Unpublished,
            Both => Both,
            _ => Published
        };
}

public static class PostGallerySize
{
    public const string Default = "default";
    public const string Compact = "compact";

    public static bool IsCompact(string? gallery) =>
        !string.Equals(gallery, Default, StringComparison.OrdinalIgnoreCase);
}
