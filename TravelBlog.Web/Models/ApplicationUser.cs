using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace TravelBlog.Web.Models;

public class ApplicationUser : IdentityUser
{
    [Required]
    [StringLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    public ICollection<Post> Posts { get; set; } = [];
}
