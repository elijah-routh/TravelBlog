namespace TravelBlog.Web.Models;

public static class PostCommentThreadMapper
{
    public static List<PostCommentItemViewModel> Build(
        IEnumerable<PostComment> comments,
        string? userId,
        bool isAdmin,
        bool canReply)
    {
        var list = comments.ToList();
        var replies = list
            .Where(comment => comment.ParentId is not null)
            .GroupBy(comment => comment.ParentId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(comment => comment.CreatedAt).ToList());

        return list
            .Where(comment => comment.ParentId is null)
            .OrderBy(comment => comment.CreatedAt)
            .Select(comment => Map(
                comment,
                replies,
                userId,
                isAdmin,
                canReply))
            .ToList();
    }

    private static PostCommentItemViewModel Map(
        PostComment comment,
        IReadOnlyDictionary<int, List<PostComment>> replies,
        string? userId,
        bool isAdmin,
        bool canReply)
    {
        var childReplies = replies.TryGetValue(comment.Id, out var children)
            ? children
            : [];

        return new PostCommentItemViewModel
        {
            Id = comment.Id,
            AuthorDisplayName = comment.Author.DisplayName,
            Body = comment.Body,
            CreatedAt = comment.CreatedAt,
            UpdatedAt = comment.UpdatedAt,
            CanEdit = OwnerAccess.IsAdminOrOwner(
                isAdmin,
                userId,
                comment.AuthorId),
            CanDelete = OwnerAccess.IsAdminOrOwner(
                isAdmin,
                userId,
                comment.AuthorId),
            CanReply = canReply,
            Replies = childReplies
                .Select(reply => Map(
                    reply,
                    replies,
                    userId,
                    isAdmin,
                    canReply: false))
                .ToList()
        };
    }
}
