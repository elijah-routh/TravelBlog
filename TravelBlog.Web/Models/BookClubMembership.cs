using System.ComponentModel.DataAnnotations;

namespace TravelBlog.Web.Models;

public class BookClubMembership
{
    public int ClubId { get; set; }

    public BookClub Club { get; set; } = null!;

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
