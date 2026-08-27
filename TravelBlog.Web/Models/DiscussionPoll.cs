using System.ComponentModel.DataAnnotations;

namespace TravelBlog.Web.Models;

public class DiscussionPoll
{
    public int Id { get; set; }

    public int DiscussionPostId { get; set; }

    public DiscussionPost DiscussionPost { get; set; } = null!;

    public ICollection<DiscussionPollOption> Options { get; set; } = [];
}

public class DiscussionPollOption
{
    public int Id { get; set; }

    public int PollId { get; set; }

    public DiscussionPoll Poll { get; set; } = null!;

    [Required]
    [StringLength(200)]
    public string Text { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public ICollection<DiscussionPollVote> Votes { get; set; } = [];
}

public class DiscussionPollVote
{
    public int Id { get; set; }

    public int PollId { get; set; }

    public int OptionId { get; set; }

    public DiscussionPollOption Option { get; set; } = null!;

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
