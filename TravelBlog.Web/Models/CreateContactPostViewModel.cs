using System.ComponentModel.DataAnnotations;

namespace TravelBlog.Web.Models;

public sealed class CreateContactPostViewModel
{
    [Required(ErrorMessage = "A title is required.")]
    [StringLength(
        150,
        ErrorMessage = "The title cannot exceed 150 characters.")]
    [Display(Name = "Title")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "A message is required.")]
    [StringLength(
        4000,
        MinimumLength = 10,
        ErrorMessage = "Your message must be between 10 and 4,000 characters.")]
    [Display(Name = "Paragraph")]
    public string Content { get; set; } = string.Empty;

    public bool Submitted { get; set; }

    public bool CanSubmit { get; set; }

    public bool HasReachedDailyLimit { get; set; }

    public bool IsBlocked { get; set; }
}
