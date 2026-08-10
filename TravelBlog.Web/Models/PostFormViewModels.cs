using System.ComponentModel.DataAnnotations;

namespace TravelBlog.Web.Models;

public abstract class PostFormViewModel
{
    [Required(ErrorMessage = "A title is required.")]
    [StringLength(
        150,
        ErrorMessage = "The title cannot exceed 150 characters.")]
    public string Title { get; set; } = string.Empty;

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
        350,
        ErrorMessage = "The summary cannot exceed 350 characters.")]
    public string? Summary { get; set; }

    [Required(ErrorMessage = "Post content is required.")]
    public string Content { get; set; } = string.Empty;

    [Display(Name = "Image path")]
    public string? ImagePath { get; set; }

    [Required(ErrorMessage = "A category is required.")]
    [EnumDataType(
        typeof(PostCategory),
        ErrorMessage = "Select a valid category.")]
    [Display(Name = "Category")]
    public PostCategory Category { get; set; } =
        PostCategory.ParodyEditorial;

    [Display(Name = "Published")]
    public bool IsPublished { get; set; }
}

public sealed class CreatePostViewModel : PostFormViewModel;

public sealed class EditPostViewModel : PostFormViewModel
{
    public int Id { get; set; }
}
