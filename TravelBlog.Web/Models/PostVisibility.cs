namespace TravelBlog.Web.Models;

public static class PostVisibility
{
    public static IQueryable<Post> PubliclyListed(this IQueryable<Post> query) =>
        query.Where(post => post.IsPublished && !post.IsHidden);

    public static bool IsPubliclyListed(Post post) =>
        post.IsPublished && !post.IsHidden;
}
