using Microsoft.AspNetCore.Mvc;

namespace TravelBlog.Web.Services;

public sealed class NutPlaceholderImages : INutPlaceholderImages
{
    private static readonly string[] ImageExtensions =
    [
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".gif"
    ];

    private readonly string[] _relativePaths;
    private readonly string _fallbackPath;

    public NutPlaceholderImages(IWebHostEnvironment environment)
    {
        var nutsDirectory = Path.Combine(
            environment.WebRootPath,
            "images",
            "Nuts");

        _relativePaths = Directory.Exists(nutsDirectory)
            ? Directory.EnumerateFiles(nutsDirectory)
                .Where(IsImageFile)
                .Select(file => $"~/images/Nuts/{Path.GetFileName(file)}")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];

        _fallbackPath = SiteBranding.NutLogoPath;
    }

    public string GetImageUrl(int postId, IUrlHelper urlHelper)
    {
        var relativePath = GetRelativePath(postId);
        return urlHelper.Content(relativePath)!;
    }

    internal string GetRelativePath(int postId)
    {
        if (_relativePaths.Length == 0)
        {
            return _fallbackPath;
        }

        var index = Mod(postId, _relativePaths.Length);
        return _relativePaths[index];
    }

    private static bool IsImageFile(string path) =>
        ImageExtensions.Contains(Path.GetExtension(path));

    private static int Mod(int value, int divisor)
    {
        var remainder = value % divisor;
        return remainder < 0 ? remainder + divisor : remainder;
    }
}
