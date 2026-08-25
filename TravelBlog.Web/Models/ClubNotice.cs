using System.ComponentModel.DataAnnotations;

namespace TravelBlog.Web.Models;

public class ClubNotice
{
    public int Id { get; set; }

    public int ClubId { get; set; }

    public BookClub Club { get; set; } = null!;

    [Required(ErrorMessage = "A notice is required.")]
    [StringLength(
        2000,
        ErrorMessage = "The notice cannot exceed 2000 characters.")]
    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public string AuthorId { get; set; } = string.Empty;

    public ApplicationUser Author { get; set; } = null!;
}
