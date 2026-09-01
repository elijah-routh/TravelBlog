using Microsoft.AspNetCore.Mvc;
using TravelBlog.Web.Models;

namespace TravelBlog.Web.Services;

public static class SiteBranding
{
    public const string NutLogoSmallPath = "~/images/NutLogoSmall.png";
    public const string NutLogoPath = "~/images/NutLogo.png";

    public const string ShareImageViewDataKey = "ShareImage";
    public const string ShareDescriptionViewDataKey = "ShareDescription";

    public const string DefaultShareDescription =
        "Giant Lampoon — literature and stuff, fiction and satire, and other writing.";

    public static string PostCardImage(
        string? imagePath,
        int postId,
        IUrlHelper urlHelper,
        INutPlaceholderImages placeholders) =>
        string.IsNullOrWhiteSpace(imagePath)
            ? placeholders.GetImageUrl(postId, urlHelper)
            : ResolveImageUrl(imagePath, urlHelper);

    public static bool IsPlaceholderImage(string? imagePath) =>
        string.IsNullOrWhiteSpace(imagePath);

    public static string ResolveImageUrl(string imagePath, IUrlHelper urlHelper) =>
        imagePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        imagePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? imagePath
            : urlHelper.Content(imagePath)!;

    public static string ShareImageForPost(
        Post post,
        IUrlHelper urlHelper,
        INutPlaceholderImages placeholders) =>
        string.IsNullOrWhiteSpace(post.ImagePath)
            ? placeholders.GetImageUrl(post.Id, urlHelper)
            : ResolveImageUrl(post.ImagePath, urlHelper);

    public static string ShareDescriptionForPost(Post post) =>
        !string.IsNullOrWhiteSpace(post.Summary)
            ? post.Summary
            : TextPreview.Truncate(post.Content, 200);

    public static void SetPostShareMetadata(
        IDictionary<string, object?> viewData,
        Post post,
        IUrlHelper urlHelper,
        INutPlaceholderImages placeholders)
    {
        viewData[ShareImageViewDataKey] = ShareImageForPost(post, urlHelper, placeholders);
        viewData[ShareDescriptionViewDataKey] = ShareDescriptionForPost(post);
    }

    public static string ToAbsoluteUrl(HttpRequest request, string imageUrl)
    {
        if (imageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            imageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return imageUrl;
        }

        var path = imageUrl.StartsWith('/') ? imageUrl : "/" + imageUrl;
        return $"{request.Scheme}://{request.Host}{path}";
    }
}
