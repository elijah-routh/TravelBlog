using System.ComponentModel.DataAnnotations;

namespace TravelBlog.Web.Models;

public class ClubBook
{
    public int Id { get; set; }

    public int ClubId { get; set; }

    public BookClub Club { get; set; } = null!;

    [Required(ErrorMessage = "A title is required.")]
    [StringLength(
        200,
        ErrorMessage = "The title cannot exceed 200 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "An author is required.")]
    [Display(Name = "Author")]
    [StringLength(
        150,
        ErrorMessage = "The author cannot exceed 150 characters.")]
    public string AuthorName { get; set; } = string.Empty;

    [StringLength(
        1000,
        ErrorMessage = "The notes cannot exceed 1000 characters.")]
    public string? Notes { get; set; }

    public string? ImagePath { get; set; }

    public string? ImageObjectKey { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Start date")]
    public DateTime StartDate { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "End date")]
    public DateTime EndDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<BookDiscussionThread> DiscussionThreads { get; set; } = [];

    public ICollection<DiscussionPost> DiscussionPosts { get; set; } = [];
}
