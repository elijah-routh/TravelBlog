using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using TravelBlog.Web.Services;

namespace TravelBlog.Web.Tests;

public sealed class NutPlaceholderImagesTests
{
    [Fact]
    public void UsesStableNutImagePerPostId()
    {
        using var environment = CreateEnvironment();
        var placeholders = new NutPlaceholderImages(environment);
        var urlHelper = CreateUrlHelper();

        var firstPath = placeholders.GetImageUrl(42, urlHelper);
        var secondPath = placeholders.GetImageUrl(42, urlHelper);

        Assert.Equal(firstPath, secondPath);
        Assert.StartsWith("/images/Nuts/", firstPath);
    }

    [Fact]
    public void DifferentPostIdsCanUseDifferentNutImages()
    {
        using var environment = CreateEnvironment();
        var placeholders = new NutPlaceholderImages(environment);
        var urlHelper = CreateUrlHelper();

        var paths = Enumerable.Range(1, 20)
            .Select(id => placeholders.GetImageUrl(id, urlHelper))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.True(paths.Length > 1);
    }

    [Fact]
    public void FallsBackToNutLogoWhenFolderIsEmpty()
    {
        using var environment = CreateEnvironment(includeNuts: false);
        var placeholders = new NutPlaceholderImages(environment);
        var urlHelper = CreateUrlHelper();

        Assert.Equal("/images/NutLogo.png", placeholders.GetImageUrl(7, urlHelper));
    }

    private static TestWebHostEnvironment CreateEnvironment(bool includeNuts = true)
    {
        var webRoot = Path.Combine(
            Path.GetTempPath(),
            "travelblog-nuts-test",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(Path.Combine(webRoot, "images", "Nuts"));
        if (includeNuts)
        {
            File.WriteAllText(
                Path.Combine(webRoot, "images", "Nuts", "AlphaNut.png"),
                string.Empty);
            File.WriteAllText(
                Path.Combine(webRoot, "images", "Nuts", "BetaNut.png"),
                string.Empty);
        }

        return new TestWebHostEnvironment(webRoot);
    }

    private static IUrlHelper CreateUrlHelper()
    {
        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new ActionDescriptor());

        return new UrlHelper(actionContext);
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment, IDisposable
    {
        public TestWebHostEnvironment(string webRootPath)
        {
            WebRootPath = webRootPath;
            ContentRootPath = webRootPath;
        }

        public string ApplicationName { get; set; } = "TravelBlog.Web.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; }
        public string EnvironmentName { get; set; } = Environments.Development;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; }

        public void Dispose()
        {
            if (Directory.Exists(WebRootPath))
            {
                Directory.Delete(WebRootPath, recursive: true);
            }
        }
    }
}
