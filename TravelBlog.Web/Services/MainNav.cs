using Microsoft.AspNetCore.Mvc.Rendering;

namespace TravelBlog.Web.Services;

public static class MainNav
{
    public static string LinkClass(ViewContext viewContext, string section) =>
        IsActive(viewContext, section) ? "nav-link is-active" : "nav-link";

    public static string? AriaCurrent(ViewContext viewContext, string section) =>
        IsActive(viewContext, section) ? "page" : null;

    public static bool IsActive(ViewContext viewContext, string section)
    {
        var area = viewContext.RouteData.Values["area"] as string;
        var controller = viewContext.RouteData.Values["controller"] as string ?? string.Empty;
        var action = viewContext.RouteData.Values["action"] as string ?? string.Empty;
        var page = viewContext.RouteData.Values["page"] as string;

        return section switch
        {
            "home" => controller.Equals("Home", StringComparison.OrdinalIgnoreCase) &&
                action.Equals("Index", StringComparison.OrdinalIgnoreCase),
            "book-clubs" => IsBookClubSection(controller),
            "posts" => controller.Equals("Posts", StringComparison.OrdinalIgnoreCase) &&
                !action.Equals("Create", StringComparison.OrdinalIgnoreCase),
            "create" => controller.Equals("Posts", StringComparison.OrdinalIgnoreCase) &&
                action.Equals("Create", StringComparison.OrdinalIgnoreCase),
            "admin" => controller.Equals("Users", StringComparison.OrdinalIgnoreCase),
            "profile" => string.Equals(area, "Identity", StringComparison.OrdinalIgnoreCase) &&
                page?.StartsWith("/Account/Manage", StringComparison.OrdinalIgnoreCase) == true,
            "register" => string.Equals(area, "Identity", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(page, "/Account/Register", StringComparison.OrdinalIgnoreCase),
            "login" => string.Equals(area, "Identity", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(page, "/Account/Login", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool IsBookClubSection(string controller) =>
        controller.Equals("BookClubs", StringComparison.OrdinalIgnoreCase) ||
        controller.Equals("ClubBooks", StringComparison.OrdinalIgnoreCase) ||
        controller.Equals("DiscussionPosts", StringComparison.OrdinalIgnoreCase) ||
        controller.Equals("DiscussionPolls", StringComparison.OrdinalIgnoreCase);
}
