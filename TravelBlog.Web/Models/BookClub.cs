using System.ComponentModel.DataAnnotations;

namespace TravelBlog.Web.Models;

public class BookClub
{
    public int Id { get; set; }

    [Required(ErrorMessage = "A name is required.")]
    [StringLength(
        150,
        ErrorMessage = "The name cannot exceed 150 characters.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "A URL slug is required.")]
    [StringLength(
        160,
        ErrorMessage = "The slug cannot exceed 160 characters.")]
    [RegularExpression(
        @"^[a-z0-9]+(?:-[a-z0-9]+)*$",
        ErrorMessage =
            "Use lowercase letters, numbers, and hyphens only.")]
    public string Slug { get; set; } = string.Empty;

    [StringLength(
        1000,
        ErrorMessage = "The description cannot exceed 1000 characters.")]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public string CreatedById { get; set; } = string.Empty;

    public ApplicationUser CreatedBy { get; set; } = null!;

    public ICollection<BookClubMembership> Memberships { get; set; } = [];

    public ICollection<ClubBook> Books { get; set; } = [];

    public ICollection<ClubNotice> Notices { get; set; } = [];

    public ICollection<DiscussionPost> DiscussionPosts { get; set; } = [];
}
