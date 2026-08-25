namespace TravelBlog.Web.Models;

public static class DiscussionThreadMapper
{
    public static List<DiscussionPostItemViewModel> Build(
        IEnumerable<DiscussionPost> posts,
        string clubSlug,
        string? userId,
        bool isAdmin,
        bool canPost)
    {
        var list = posts.ToList();
        var replies = list
            .Where(post => post.ParentId is not null)
            .GroupBy(post => post.ParentId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(post => post.CreatedAt).ToList());

        return list
            .Where(post => post.ParentId is null)
            .OrderBy(post => post.CreatedAt)
            .Select(post => Map(
                post,
                clubSlug,
                replies,
                userId,
                isAdmin,
                canReply: canPost))
            .ToList();
    }

    private static DiscussionPostItemViewModel Map(
        DiscussionPost post,
        string clubSlug,
        IReadOnlyDictionary<int, List<DiscussionPost>> replies,
        string? userId,
        bool isAdmin,
        bool canReply)
    {
        var childReplies = replies.TryGetValue(post.Id, out var children)
            ? children
            : [];

        return new DiscussionPostItemViewModel
        {
            Id = post.Id,
            ClubSlug = clubSlug,
            AuthorDisplayName = post.Author.DisplayName,
            Body = post.Body,
            CreatedAt = post.CreatedAt,
            UpdatedAt = post.UpdatedAt,
            CanEdit = OwnerAccess.IsAdminOrOwner(isAdmin, userId, post.AuthorId),
            CanDelete = OwnerAccess.IsAdminOrOwner(isAdmin, userId, post.AuthorId),
            CanReply = canReply,
            Replies = childReplies
                .Select(reply => Map(
                    reply,
                    clubSlug,
                    replies,
                    userId,
                    isAdmin,
                    canReply: false))
                .ToList()
        };
    }
}

public static class OwnerAccess
{
    public static bool IsAdminOrOwner(
        bool isAdmin,
        string? userId,
        string authorId) =>
        isAdmin ||
        (!string.IsNullOrWhiteSpace(userId) && authorId == userId);
}
