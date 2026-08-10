using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using TravelBlog.Web.Models;

namespace TravelBlog.Web.Authorization;

public sealed class PostOwnerOrAdminHandler
    : AuthorizationHandler<PostOwnerOrAdminRequirement, Post>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public PostOwnerOrAdminHandler(
        UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PostOwnerOrAdminRequirement requirement,
        Post resource)
    {
        var userId = _userManager.GetUserId(context.User);

        if (context.User.IsInRole(RoleNames.Admin) ||
            (!string.IsNullOrWhiteSpace(userId) &&
             resource.AuthorId == userId))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
