using TravelBlog.Web.Models;

namespace TravelBlog.Web.Services;

public static class UserWriteAccess
{
    public static bool CanWriteContent(ApplicationUser? user) =>
        user?.EmailConfirmed == true && !user.IsBlocked;
}
