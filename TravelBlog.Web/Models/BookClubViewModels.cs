using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace TravelBlog.Web.Models;

public class BookClubFormViewModel
{
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
    [Display(Name = "URL slug")]
    public string Slug { get; set; } = string.Empty;

    [StringLength(
        1000,
        ErrorMessage = "The description cannot exceed 1000 characters.")]
    public string? Description { get; set; }
}

public class AddClubBookViewModel : IValidatableObject
{
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

    [Display(Name = "Book cover")]
    public IFormFile? CoverImage { get; set; }

    public string? CurrentImagePath { get; set; }

    [Required(ErrorMessage = "A start date is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "Start date")]
    public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;

    [Required(ErrorMessage = "An end date is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "End date")]
    public DateTime EndDate { get; set; } = DateTime.UtcNow.Date;

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (EndDate.Date < StartDate.Date)
        {
            yield return new ValidationResult(
                "The end date must be on or after the start date.",
                [nameof(EndDate)]);
        }
    }
}

public class AddClubNoticeViewModel
{
    [Required(ErrorMessage = "A notice is required.")]
    [StringLength(
        2000,
        ErrorMessage = "The notice cannot exceed 2000 characters.")]
    [Display(Name = "Notice")]
    public string Body { get; set; } = string.Empty;
}

public class AddDiscussionPostViewModel
{
    [Required(ErrorMessage = "A message is required.")]
    [StringLength(
        2000,
        ErrorMessage = "The message cannot exceed 2000 characters.")]
    [Display(Name = "Message")]
    public string Body { get; set; } = string.Empty;
}

public class CreateDiscussionPollViewModel : IValidatableObject
{
    [Required(ErrorMessage = "A poll title is required.")]
    [StringLength(
        200,
        ErrorMessage = "The poll title cannot exceed 200 characters.")]
    public string Title { get; set; } = string.Empty;

    public List<string> Options { get; set; } = ["", ""];

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        var options = Options
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Select(option => option.Trim())
            .ToList();
        if (options.Count < 2)
        {
            yield return new ValidationResult(
                "Add at least two poll options.",
                [nameof(Options)]);
        }
        if (options.Count > 10)
        {
            yield return new ValidationResult(
                "A poll cannot have more than 10 options.",
                [nameof(Options)]);
        }
        if (options.Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
            options.Count)
        {
            yield return new ValidationResult(
                "Poll options must be unique.",
                [nameof(Options)]);
        }
    }
}

public class CreateBookDiscussionThreadViewModel
{
    [Required(ErrorMessage = "A thread title is required.")]
    [StringLength(
        100,
        ErrorMessage = "The thread title cannot exceed 100 characters.")]
    [Display(Name = "Thread title")]
    public string Title { get; set; } = string.Empty;
}

public class BookClubListItemViewModel
{
    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int MemberCount { get; set; }

    public int? CurrentBookId { get; set; }

    public string? CurrentBookTitle { get; set; }

    public string? CurrentBookImagePath { get; set; }

    public DateTime? CurrentBookStartDate { get; set; }

    public DateTime? CurrentBookEndDate { get; set; }
}

public class BookClubIndexViewModel
{
    public IReadOnlyList<BookClubListItemViewModel> Clubs { get; set; } = [];

    public bool IsAdmin { get; set; }
}

public class CombinedBookTimelineItemViewModel
{
    public int Id { get; set; }

    public string ClubName { get; set; } = string.Empty;

    public string ClubSlug { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string AuthorName { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public string? ImagePath { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public string Status { get; set; } = string.Empty;
}

public class CombinedBookTimelineViewModel
{
    public IReadOnlyList<CombinedBookTimelineItemViewModel> Books { get; set; } =
        [];
}

public class ClubNoticeItemViewModel
{
    public int Id { get; set; }

    public string AuthorDisplayName { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

public class DiscussionPostItemViewModel
{
    public int Id { get; set; }

    public string ClubSlug { get; set; } = string.Empty;

    public string AuthorDisplayName { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool CanEdit { get; set; }

    public bool CanDelete { get; set; }

    public bool CanReply { get; set; }

    public bool CanPin { get; set; }

    public bool IsPinned { get; set; }

    public DiscussionPollItemViewModel? Poll { get; set; }

    public IReadOnlyList<DiscussionPostItemViewModel> Replies { get; set; } = [];
}

public class DiscussionPollItemViewModel
{
    public int Id { get; set; }

    public string ClubSlug { get; set; } = string.Empty;

    public int TotalVotes { get; set; }

    public bool CanVote { get; set; }

    public IReadOnlyList<DiscussionPollOptionItemViewModel> Options
        { get; set; } = [];
}

public class DiscussionPollOptionItemViewModel
{
    public int Id { get; set; }

    public string Text { get; set; } = string.Empty;

    public bool IsSelectedByCurrentUser { get; set; }

    public IReadOnlyList<string> VoterDisplayNames { get; set; } = [];
}

public class DiscussionThreadViewModel
{
    public string ClubSlug { get; set; } = string.Empty;

    public IReadOnlyList<DiscussionPostItemViewModel> Posts { get; set; } = [];
}

public class EditDiscussionPostViewModel
{
    public int Id { get; set; }

    public string ClubSlug { get; set; } = string.Empty;

    public int? ClubBookId { get; set; }

    [Required(ErrorMessage = "A message is required.")]
    [StringLength(
        2000,
        ErrorMessage = "The message cannot exceed 2000 characters.")]
    [Display(Name = "Message")]
    public string Body { get; set; } = string.Empty;
}

public class ClubBookTimelineItemViewModel
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string AuthorName { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public string? ImagePath { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public string Status { get; set; } = string.Empty;
}

public class BookClubDetailsViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int MemberCount { get; set; }

    public bool IsMember { get; set; }

    public bool CanPost { get; set; }

    public bool IsAdmin { get; set; }

    public bool ShowTimeline { get; set; }

    public string DiscussionSort { get; set; } = DiscussionSortOrder.Newest;

    public ClubBookTimelineItemViewModel? CurrentBook { get; set; }

    public IReadOnlyList<ClubNoticeItemViewModel> Notices { get; set; } = [];

    public IReadOnlyList<ClubBookTimelineItemViewModel> Books { get; set; } = [];

    public IReadOnlyList<DiscussionPostItemViewModel> DiscussionPosts { get; set; } =
        [];

    public AddClubNoticeViewModel NewNotice { get; set; } = new();

    public AddDiscussionPostViewModel NewDiscussion { get; set; } = new();

    public CreateDiscussionPollViewModel NewPoll { get; set; } = new();
}

public class ClubBookDetailsViewModel
{
    public int Id { get; set; }

    public string ClubName { get; set; } = string.Empty;

    public string ClubSlug { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string AuthorName { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public string? ImagePath { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public string Status { get; set; } = string.Empty;

    public bool CanPost { get; set; }

    public bool IsAdmin { get; set; }

    public bool IsAuthenticated { get; set; }

    public string DiscussionSort { get; set; } = DiscussionSortOrder.Newest;

    public int ActiveThreadId { get; set; }

    public IReadOnlyList<BookDiscussionThreadViewModel> DiscussionThreads
        { get; set; } = [];

    public AddDiscussionPostViewModel NewDiscussion { get; set; } = new();

    public CreateBookDiscussionThreadViewModel NewThread { get; set; } = new();

    public CreateDiscussionPollViewModel NewPoll { get; set; } = new();
}

public class BookDiscussionThreadViewModel
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public bool CanDelete { get; set; }

    public IReadOnlyList<DiscussionPostItemViewModel> Posts { get; set; } = [];
}

public class ClubBookDeleteViewModel
{
    public int Id { get; set; }

    public string ClubName { get; set; } = string.Empty;

    public string ClubSlug { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string AuthorName { get; set; } = string.Empty;
}

public class PostCommentItemViewModel
{
    public int Id { get; set; }

    public string AuthorDisplayName { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool CanEdit { get; set; }

    public bool CanDelete { get; set; }

    public bool CanReply { get; set; }

    public IReadOnlyList<PostCommentItemViewModel> Replies { get; set; } = [];
}

public class AddPostCommentViewModel
{
    [Required(ErrorMessage = "A comment is required.")]
    [StringLength(
        2000,
        ErrorMessage = "The comment cannot exceed 2000 characters.")]
    [Display(Name = "Comment")]
    public string Body { get; set; } = string.Empty;
}

public class PostDetailsViewModel
{
    public Post Post { get; set; } = null!;

    public IReadOnlyList<PostCommentItemViewModel> Comments { get; set; } = [];

    public AddPostCommentViewModel NewComment { get; set; } = new();

    public bool CanComment { get; set; }

    public bool IsAuthenticated { get; set; }
}

public class EditPostCommentViewModel
{
    public int Id { get; set; }

    public string PostSlug { get; set; } = string.Empty;

    [Required(ErrorMessage = "A comment is required.")]
    [StringLength(
        2000,
        ErrorMessage = "The comment cannot exceed 2000 characters.")]
    [Display(Name = "Comment")]
    public string Body { get; set; } = string.Empty;
}
