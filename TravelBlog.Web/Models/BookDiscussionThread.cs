using System.ComponentModel.DataAnnotations;

namespace TravelBlog.Web.Models;

public class BookDiscussionThread
{
    public int Id { get; set; }

    public int ClubBookId { get; set; }

    public ClubBook ClubBook { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string Title { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DiscussionPost> Posts { get; set; } = [];
}
