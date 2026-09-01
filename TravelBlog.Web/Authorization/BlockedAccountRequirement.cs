using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using TravelBlog.Web.Models;

namespace TravelBlog.Web.Authorization;

public sealed class BlockedAccountRequirement : IAuthorizationRequirement;

public sealed class BlockedAccountHandler(
    UserManager<ApplicationUser> userManager)
    : AuthorizationHandler<BlockedAccountRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        BlockedAccountRequirement requirement)
    {
        var user = await userManager.GetUserAsync(context.User);
        if (user is not null && !user.IsBlocked)
        {
            context.Succeed(requirement);
        }
    }
}
