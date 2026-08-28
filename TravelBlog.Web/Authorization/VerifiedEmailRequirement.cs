using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TravelBlog.Web.Models;

namespace TravelBlog.Web.Authorization;

public sealed class VerifiedEmailRequirement : IAuthorizationRequirement;

public sealed class VerifiedEmailHandler(
    UserManager<ApplicationUser> userManager)
    : AuthorizationHandler<VerifiedEmailRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        VerifiedEmailRequirement requirement)
    {
        var user = await userManager.GetUserAsync(context.User);
        if (user?.EmailConfirmed == true)
        {
            context.Succeed(requirement);
        }
    }
}

public sealed class VerifiedMutationFilter(
    IAuthorizationService authorizationService) : IAsyncAuthorizationFilter
{
    private static readonly HashSet<string> ProtectedControllers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Posts",
            "BookClubs",
            "ClubBooks",
            "DiscussionPosts",
            "DiscussionPolls",
            "Users"
        };
    private static readonly HashSet<string> WritePageActions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Create",
            "Edit",
            "Delete",
            "EditComment",
            "DeleteComment"
        };

    public async Task OnAuthorizationAsync(
        AuthorizationFilterContext context)
    {
        var controller =
            context.RouteData.Values["controller"]?.ToString() ?? "";
        var action = context.RouteData.Values["action"]?.ToString() ?? "";
        var isWritePage = HttpMethods.IsGet(
                context.HttpContext.Request.Method) &&
            WritePageActions.Contains(action);
        var isPostCreatePrompt = HttpMethods.IsGet(
                context.HttpContext.Request.Method) &&
            controller.Equals("Posts", StringComparison.OrdinalIgnoreCase) &&
            action.Equals("Create", StringComparison.OrdinalIgnoreCase);
        if (isPostCreatePrompt)
        {
            return;
        }

        if ((!HttpMethods.IsPost(context.HttpContext.Request.Method) &&
             !isWritePage) ||
            !ProtectedControllers.Contains(controller))
        {
            return;
        }

        var result = await authorizationService.AuthorizeAsync(
            context.HttpContext.User,
            resource: null,
            PolicyNames.VerifiedEmail);
        if (!result.Succeeded)
        {
            context.Result = context.HttpContext.User.Identity?.IsAuthenticated
                == true
                ? new ForbidResult()
                : new ChallengeResult();
        }
    }
}
