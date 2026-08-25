using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace TravelBlog.Web.Models;

public class ApplicationUser : IdentityUser
{
    [Required]
    [StringLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    public ICollection<Post> Posts { get; set; } = [];

    public ICollection<BookClub> CreatedBookClubs { get; set; } = [];

    public ICollection<BookClubMembership> BookClubMemberships { get; set; } = [];

    public ICollection<ClubNotice> ClubNotices { get; set; } = [];

    public ICollection<DiscussionPost> DiscussionPosts { get; set; } = [];

    public ICollection<PostComment> PostComments { get; set; } = [];
}
