using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TravelBlog.Web.Data;
using TravelBlog.Web.Models;

namespace TravelBlog.Web.Tests;

[Collection(IntegrationTestCollection.Name)]
public sealed class PostsIndexTests
{
    private const string Password = "Test-pass1!";
    private readonly TravelBlogWebApplicationFactory _factory;

    public PostsIndexTests(TravelBlogWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task IndexUsesCompactGalleryByDefault()
    {
        await EnsurePublishedPostAsync("compact-gallery");
        using var client = CreateClient();
        var html = await client.GetStringAsync("/Posts");

        Assert.Contains("posts-grid--compact", html);
        Assert.DoesNotContain("gallery=default", html);
        Assert.Contains("Most liked", html);
        Assert.Contains($"value=\"{PostSortOrder.MostLiked}\"", html);
    }

    [Fact]
    public async Task IndexCanOptIntoLargerCards()
    {
        await EnsurePublishedPostAsync("large-gallery");
        using var client = CreateClient();
        var html = await client.GetStringAsync("/Posts?gallery=default");

        Assert.DoesNotContain("posts-grid--compact", html);
        Assert.Contains("posts-grid", html);
    }

    [Fact]
    public async Task IndexCanSortByMostLiked()
    {
        var author = await CreateUserAsync("Liked Sort Author");
        var likerOne = await CreateUserAsync("Liked Sort One");
        var likerTwo = await CreateUserAsync("Liked Sort Two");
        var quiet = await CreatePostAsync(author, "liked-sort-quiet", daysAgo: 0);
        var middle = await CreatePostAsync(author, "liked-sort-mid", daysAgo: 1);
        var popular = await CreatePostAsync(author, "liked-sort-hot", daysAgo: 2);
        await AddLikeAsync(popular, likerOne);
        await AddLikeAsync(popular, likerTwo);
        await AddLikeAsync(middle, likerOne);

        using var client = CreateClient();
        var html = await client.GetStringAsync(
            $"/Posts?sort={PostSortOrder.MostLiked}");

        Assert.Contains("id=\"sort-liked\"", html);
        Assert.Contains("checked", html);
        var popularAt = html.IndexOf(popular.Title, StringComparison.Ordinal);
        var middleAt = html.IndexOf(middle.Title, StringComparison.Ordinal);
        var quietAt = html.IndexOf(quiet.Title, StringComparison.Ordinal);
        Assert.True(popularAt >= 0 && middleAt >= 0 && quietAt >= 0);
        Assert.True(popularAt < middleAt);
        Assert.True(middleAt < quietAt);
    }

    [Fact]
    public async Task IndexCardsUseLikeButtons()
    {
        await EnsurePublishedPostAsync("card-like-button");
        using var client = CreateClient();
        var html = await client.GetStringAsync("/Posts");

        Assert.Contains("like-action", html);
        Assert.Contains("Log in to like", html);
        Assert.Contains("post-view-count", html);
        Assert.DoesNotContain("post-card-engagement", html);
    }

    [Fact]
    public async Task UserCanLikeFromIndexAndStayOnList()
    {
        var author = await CreateUserAsync("Card Like Author");
        var liker = await CreateUserAsync("Card Liker");
        var post = await CreatePostAsync(author, "card-like", daysAgo: 0);
        using var client = CreateLoginClient();
        var token = await GetAntiforgeryTokenAsync(
            client,
            "/Identity/Account/Login");
        var login = await client.PostAsync(
            "/Identity/Account/Login",
            Form(token,
                ("Input.Email", liker.Email!),
                ("Input.Password", Password),
                ("Input.RememberMe", "false")));
        Assert.Equal(System.Net.HttpStatusCode.Redirect, login.StatusCode);

        var listToken = await GetAntiforgeryTokenAsync(client, "/Posts");
        var listHtml = await client.GetStringAsync("/Posts");
        Assert.Contains("ToggleLike", listHtml);
        Assert.Contains("like-action", listHtml);

        var like = await client.PostAsync(
            $"/Posts/ToggleLike?slug={post.Slug}&from=index",
            Form(listToken));

        Assert.Equal(HttpStatusCode.Redirect, like.StatusCode);
        Assert.StartsWith("/Posts", like.Headers.Location?.OriginalString);
        Assert.DoesNotContain("Details", like.Headers.Location?.OriginalString);
        await WithServicesAsync(async services =>
        {
            Assert.True(await services
                .GetRequiredService<BlogDbContext>()
                .PostLikes
                .AnyAsync(existing =>
                    existing.PostId == post.Id &&
                    existing.UserId == liker.Id));
        });
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });

    private HttpClient CreateLoginClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });

    private static async Task<string> GetAntiforgeryTokenAsync(
        HttpClient client,
        string path)
    {
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, $"No antiforgery token found at {path}.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static FormUrlEncodedContent Form(
        string token,
        params (string Key, string Value)[] values)
    {
        var fields = values
            .Select(value =>
                new KeyValuePair<string, string>(value.Key, value.Value))
            .Append(new KeyValuePair<string, string>(
                "__RequestVerificationToken",
                token));
        return new FormUrlEncodedContent(fields);
    }

    private async Task<ApplicationUser> CreateUserAsync(string displayName)
    {
        ApplicationUser? created = null;
        await WithServicesAsync(async services =>
        {
            var userManager =
                services.GetRequiredService<UserManager<ApplicationUser>>();
            created = new ApplicationUser
            {
                DisplayName = displayName,
                UserName = UniqueEmail("user"),
                EmailConfirmed = true
            };
            created.Email = created.UserName;
            Assert.True(
                (await userManager.CreateAsync(created, Password)).Succeeded);
        });
        return created!;
    }

    private async Task EnsurePublishedPostAsync(string label)
    {
        var author = await CreateUserAsync($"{label} Author");
        await CreatePostAsync(author, label, daysAgo: 0);
    }

    private async Task<Post> CreatePostAsync(
        ApplicationUser author,
        string label,
        int daysAgo)
    {
        Post? created = null;
        await WithServicesAsync(async services =>
        {
            var context = services.GetRequiredService<BlogDbContext>();
            created = new Post
            {
                AuthorId = author.Id,
                Title = $"{label}-{Guid.NewGuid():N}",
                Slug = $"{label}-{Guid.NewGuid():N}",
                Content = "Most liked sort test.",
                Category = PostCategory.LiteratureAndStuff,
                IsPublished = true,
                CreatedAt = DateTime.UtcNow.AddDays(-daysAgo)
            };
            context.Posts.Add(created);
            await context.SaveChangesAsync();
        });
        return created!;
    }

    private async Task AddLikeAsync(Post post, ApplicationUser user)
    {
        await WithServicesAsync(async services =>
        {
            var context = services.GetRequiredService<BlogDbContext>();
            context.PostLikes.Add(new PostLike
            {
                PostId = post.Id,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        });
    }

    private async Task WithServicesAsync(Func<IServiceProvider, Task> action)
    {
        using var scope = _factory.Services.CreateScope();
        await action(scope.ServiceProvider);
    }

    private static string UniqueEmail(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}@example.test";
}
