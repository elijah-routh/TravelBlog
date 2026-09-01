using TravelBlog.Web.Models;

namespace TravelBlog.Web.Tests;

public sealed class PostGallerySizeTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("compact", true)]
    [InlineData("default", false)]
    public void IsCompact_TreatsCompactAsDefault(string? gallery, bool expected) =>
        Assert.Equal(expected, PostGallerySize.IsCompact(gallery));
}

public sealed class PostSortOrderTests
{
    [Theory]
    [InlineData(null, PostSortOrder.Newest)]
    [InlineData("newest", PostSortOrder.Newest)]
    [InlineData("oldest", PostSortOrder.Oldest)]
    [InlineData("liked", PostSortOrder.MostLiked)]
    [InlineData("LIKED", PostSortOrder.MostLiked)]
    [InlineData("unknown", PostSortOrder.Newest)]
    public void Normalize_AcceptsMostLiked(string? sort, string expected) =>
        Assert.Equal(expected, PostSortOrder.Normalize(sort));
}
