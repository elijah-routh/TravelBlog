using Microsoft.EntityFrameworkCore;
using TravelBlog.Web.Data;

namespace TravelBlog.Web.Services;

public static class EngagementQueries
{
    public static async Task<(
        IReadOnlyDictionary<int, int> Likes,
        IReadOnlyDictionary<int, int> Views)> CountPostsAsync(
        BlogDbContext context,
        IReadOnlyCollection<int> postIds)
    {
        if (postIds.Count == 0)
        {
            return (
                new Dictionary<int, int>(),
                new Dictionary<int, int>());
        }

        var likes = await context.PostLikes
            .AsNoTracking()
            .Where(like => postIds.Contains(like.PostId))
            .GroupBy(like => like.PostId)
            .ToDictionaryAsync(group => group.Key, group => group.Count());
        var views = await context.PostViews
            .AsNoTracking()
            .Where(view => postIds.Contains(view.PostId))
            .GroupBy(view => view.PostId)
            .ToDictionaryAsync(group => group.Key, group => group.Count());
        return (likes, views);
    }
}
