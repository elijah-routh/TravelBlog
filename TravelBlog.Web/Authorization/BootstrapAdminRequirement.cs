using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using TravelBlog.Web.Data;

namespace TravelBlog.Web.Authorization;

public static class BootstrapAdminAccess
{
    public static bool IsBootstrapAdmin(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) ==
        BootstrapAdminConstants.UserId;
}

public sealed class BootstrapAdminRequirement : IAuthorizationRequirement;

public sealed class BootstrapAdminHandler
    : AuthorizationHandler<BootstrapAdminRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        BootstrapAdminRequirement requirement)
    {
        if (BootstrapAdminAccess.IsBootstrapAdmin(context.User))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
