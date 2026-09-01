using System.ComponentModel.DataAnnotations;

namespace TravelBlog.Web.Models;

public class PostLike
{
    public int Id { get; set; }

    public int PostId { get; set; }

    public Post Post { get; set; } = null!;

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PostCommentLike
{
    public int Id { get; set; }

    public int PostCommentId { get; set; }

    public PostComment PostComment { get; set; } = null!;

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class DiscussionPostLike
{
    public int Id { get; set; }

    public int DiscussionPostId { get; set; }

    public DiscussionPost DiscussionPost { get; set; } = null!;

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PostView
{
    public int Id { get; set; }

    public int PostId { get; set; }

    public Post Post { get; set; } = null!;

    public string? UserId { get; set; }

    public ApplicationUser? User { get; set; }

    [Required]
    [StringLength(32)]
    public string ViewerKey { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class LikeButtonViewModel
{
    public int Count { get; init; }

    public bool IsLiked { get; init; }

    public bool CanLike { get; init; }

    public string Controller { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public Dictionary<string, string> RouteValues { get; init; } = [];
}
