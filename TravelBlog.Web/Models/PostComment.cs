using System.ComponentModel.DataAnnotations;

namespace TravelBlog.Web.Models;

public class PostComment
{
    public int Id { get; set; }

    public int PostId { get; set; }

    public Post Post { get; set; } = null!;

    [Required(ErrorMessage = "A comment is required.")]
    [StringLength(
        2000,
        ErrorMessage = "The comment cannot exceed 2000 characters.")]
    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public int? ParentId { get; set; }

    public PostComment? Parent { get; set; }

    public ICollection<PostComment> Replies { get; set; } = [];

    [Required]
    public string AuthorId { get; set; } = string.Empty;

    public ApplicationUser Author { get; set; } = null!;

    public ICollection<PostCommentLike> Likes { get; set; } = [];
}
