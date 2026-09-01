using System.ComponentModel.DataAnnotations;

namespace TravelBlog.Web.Models;

public static class PostCategories
{
    public static bool IsContact(PostCategory category) =>
        category == PostCategory.Contact;

    public static IQueryable<Post> ExcludeContact(this IQueryable<Post> query) =>
        query.Where(post => post.Category != PostCategory.Contact);

    public static IEnumerable<PostCategory> EditorialCategories() =>
        Enum.GetValues<PostCategory>()
            .Where(category => !IsContact(category));

    public static string GetDisplayName(PostCategory category)
    {
        var field = typeof(PostCategory).GetField(category.ToString());
        var display = field?
            .GetCustomAttributes(typeof(DisplayAttribute), false)
            .OfType<DisplayAttribute>()
            .SingleOrDefault();
        return display?.Name ?? category.ToString();
    }
}
