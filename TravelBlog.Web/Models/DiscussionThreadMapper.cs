namespace TravelBlog.Web.Models;

public static class DiscussionThreadMapper
{
    public static List<DiscussionPostItemViewModel> Build(
        IEnumerable<DiscussionPost> posts,
        string clubSlug,
        string? userId,
        bool isAdmin,
        bool canPost,
        string sort = DiscussionSortOrder.Newest)
    {
        var list = posts.ToList();
        var newestFirst = DiscussionSortOrder.Normalize(sort) ==
            DiscussionSortOrder.Newest;
        var replies = list
            .Where(post => post.ParentId is not null)
            .GroupBy(post => post.ParentId!.Value)
            .ToDictionary(
                group => group.Key,
                group => newestFirst
                    ? group.OrderByDescending(post => post.CreatedAt).ToList()
                    : group.OrderBy(post => post.CreatedAt).ToList());

        var topLevelPosts = list.Where(post => post.ParentId is null);
        topLevelPosts = newestFirst
            ? topLevelPosts
                .OrderByDescending(post => post.IsPinned)
                .ThenByDescending(post => post.CreatedAt)
            : topLevelPosts
                .OrderByDescending(post => post.IsPinned)
                .ThenBy(post => post.CreatedAt);

        return topLevelPosts
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
            IsPinned = post.IsPinned,
            CanPin = isAdmin && post.ParentId is null,
            CanEdit = post.Poll is null &&
                OwnerAccess.IsAdminOrOwner(isAdmin, userId, post.AuthorId),
            CanDelete = OwnerAccess.IsAdminOrOwner(isAdmin, userId, post.AuthorId),
            CanReply = canReply && post.Poll is null,
            Poll = post.Poll is null
                ? null
                : new DiscussionPollItemViewModel
                {
                    Id = post.Poll.Id,
                    ClubSlug = clubSlug,
                    TotalVotes = post.Poll.Options.Sum(option =>
                        option.Votes.Count),
                    CanVote = canReply,
                    Options = post.Poll.Options
                        .OrderBy(option => option.SortOrder)
                        .Select(option =>
                            new DiscussionPollOptionItemViewModel
                            {
                                Id = option.Id,
                                Text = option.Text,
                                IsSelectedByCurrentUser =
                                    !string.IsNullOrWhiteSpace(userId) &&
                                    option.Votes.Any(vote =>
                                        vote.UserId == userId),
                                VoterDisplayNames = option.Votes
                                    .OrderBy(vote => vote.User.DisplayName)
                                    .Select(vote => vote.User.DisplayName)
                                    .ToList()
                            })
                        .ToList()
                },
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

public static class DiscussionSortOrder
{
    public const string Oldest = "oldest";
    public const string Newest = "newest";

    public static string Normalize(string? sort) =>
        string.Equals(sort, Oldest, StringComparison.OrdinalIgnoreCase)
            ? Oldest
            : Newest;
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
