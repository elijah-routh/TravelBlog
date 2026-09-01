using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TravelBlog.Web.Data;
using TravelBlog.Web.Models;

namespace TravelBlog.Web.Tests;

[Collection(IntegrationTestCollection.Name)]
public sealed class SiteBrandingTests
{
    private readonly TravelBlogWebApplicationFactory _factory;

    public SiteBrandingTests(TravelBlogWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LayoutUsesNutLogoSmallForFaviconAndNutLogoForDefaultShareImage()
    {
        using var client = CreateClient();
        var html = await client.GetStringAsync("/");

        Assert.Contains("rel=\"icon\"", html);
        Assert.Contains("/images/NutLogoSmall.png", html);
        Assert.Contains("rel=\"apple-touch-icon\"", html);
        Assert.Contains("property=\"og:image\"", html);
        Assert.Contains("/images/NutLogo.png", html);
        Assert.Contains("name=\"twitter:image\"", html);
    }

    [Fact]
    public async Task PostWithoutImageUsesRandomNutPlaceholderOnIndexAndDetails()
    {
        var author = await CreateUserAsync("Branding Author");
        var post = await CreatePostAsync(author, "no-image");

        using var client = CreateClient();
        var indexHtml = await client.GetStringAsync("/Posts");
        Assert.Contains("/images/Nuts/", indexHtml);
        Assert.Contains("post-card-image--placeholder", indexHtml);

        var detailsHtml = await client.GetStringAsync($"/Posts/Details?slug={post.Slug}");
        Assert.Contains("/images/Nuts/", detailsHtml);
        Assert.Contains("post-card-image--placeholder", detailsHtml);
        Assert.Contains("property=\"og:image\" content=\"https://localhost/images/Nuts/", detailsHtml);
    }

    [Fact]
    public async Task PostWithImageUsesFeaturedImageForSharePreview()
    {
        var author = await CreateUserAsync("Featured Author");
        var post = await CreatePostAsync(
            author,
            "with-image",
            imagePath: "https://images.test/featured.jpg");

        using var client = CreateClient();
        var html = await client.GetStringAsync($"/Posts/Details?slug={post.Slug}");

        Assert.Contains("https://images.test/featured.jpg", html);
        Assert.Contains(
            "property=\"og:image\" content=\"https://images.test/featured.jpg\"",
            html);
        Assert.DoesNotContain("post-card-image--placeholder", html);
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });

    private async Task<ApplicationUser> CreateUserAsync(string displayName)
    {
        ApplicationUser? created = null;
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        created = new ApplicationUser
        {
            DisplayName = displayName,
            UserName = $"{displayName.Replace(' ', '-')}-{Guid.NewGuid():N}@example.test",
            EmailConfirmed = true
        };
        created.Email = created.UserName;
        Assert.True((await userManager.CreateAsync(created, "Test-pass1!")).Succeeded);
        return created;
    }

    private async Task<Post> CreatePostAsync(
        ApplicationUser author,
        string label,
        string? imagePath = null)
    {
        Post? created = null;
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
        created = new Post
        {
            AuthorId = author.Id,
            Title = $"{label}-{Guid.NewGuid():N}",
            Slug = $"{label}-{Guid.NewGuid():N}",
            Content = "Branding test post content.",
            Category = PostCategory.LiteratureAndStuff,
            IsPublished = true,
            ImagePath = imagePath,
            CreatedAt = DateTime.UtcNow
        };
        context.Posts.Add(created);
        await context.SaveChangesAsync();
        return created;
    }
}
