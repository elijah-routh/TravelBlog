using System.ComponentModel.DataAnnotations;

namespace TravelBlog.Web.Models;

public class DiscussionPost
{
    public int Id { get; set; }

    public int ClubId { get; set; }

    public BookClub Club { get; set; } = null!;

    public int? ClubBookId { get; set; }

    public ClubBook? ClubBook { get; set; }

    public int? BookDiscussionThreadId { get; set; }

    public BookDiscussionThread? BookDiscussionThread { get; set; }

    [Required(ErrorMessage = "A message is required.")]
    [StringLength(
        2000,
        ErrorMessage = "The message cannot exceed 2000 characters.")]
    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public bool IsPinned { get; set; }

    public int? ParentId { get; set; }

    public DiscussionPost? Parent { get; set; }

    public ICollection<DiscussionPost> Replies { get; set; } = [];

    public DiscussionPoll? Poll { get; set; }

    [Required]
    public string AuthorId { get; set; } = string.Empty;

    public ApplicationUser Author { get; set; } = null!;
}
