using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TravelBlog.Web.Authorization;
using TravelBlog.Web.Data;
using TravelBlog.Web.Models;

namespace TravelBlog.Web.Tests;

[Collection(IntegrationTestCollection.Name)]
public sealed class HiddenPostTests
{
    private const string Password = "Test-pass1!";
    private readonly TravelBlogWebApplicationFactory _factory;

    public HiddenPostTests(TravelBlogWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HiddenPublishedPostIsNotListedPublicly()
    {
        var author = await CreateUserAsync("Hidden Author");
        var post = await CreatePostAsync(author, "hidden-public", isHidden: true);

        using var client = CreateClient();
        var html = await client.GetStringAsync("/Posts");

        Assert.DoesNotContain(post.Title, html);
    }

    [Fact]
    public async Task HiddenPublishedPostIsVisibleToAuthorAndAdmin()
    {
        var author = await CreateUserAsync("Hidden Owner");
        var admin = await CreateUserAsync("Hidden Admin", isAdmin: true);
        var other = await CreateUserAsync("Hidden Other");
        var post = await CreatePostAsync(author, "hidden-access", isHidden: true);

        using (var authorClient = await LoginAsync(author.Email!))
        {
            using var response = await authorClient.GetAsync(
                $"/Posts/Details?slug={post.Slug}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using (var adminClient = await LoginAsync(admin.Email!))
        {
            using var response = await adminClient.GetAsync(
                $"/Posts/Details?slug={post.Slug}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using var otherClient = await LoginAsync(other.Email!);
        using var denied = await otherClient.GetAsync(
            $"/Posts/Details?slug={post.Slug}");
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }

    [Fact]
    public async Task AdminCanHideAndUnhidePublishedPost()
    {
        var author = await CreateUserAsync("Moderated Author");
        var admin = await CreateUserAsync("Moderator", isAdmin: true);
        var post = await CreatePostAsync(author, "moderated-post");

        using var adminClient = await LoginAsync(admin.Email!);
        var hideToken = await GetAntiforgeryTokenAsync(
            adminClient,
            $"/Posts/Details?slug={post.Slug}");

        using (var hideResponse = await adminClient.PostAsync(
            "/Posts/Hide",
            Form(hideToken, ("id", post.Id.ToString()))))
        {
            Assert.Equal(HttpStatusCode.Redirect, hideResponse.StatusCode);
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
        var         hidden = await context.Posts.SingleAsync(candidate => candidate.Id == post.Id);
        Assert.True(hidden.IsHidden);

        using var publicClient = CreateClient();
        using var publicResponse = await publicClient.GetAsync(
            $"/Posts/Details?slug={post.Slug}");
        Assert.Equal(HttpStatusCode.NotFound, publicResponse.StatusCode);

        var unhideToken = await GetAntiforgeryTokenAsync(
            adminClient,
            $"/Posts/Details?slug={post.Slug}");
        using (var unhideResponse = await adminClient.PostAsync(
            "/Posts/Unhide",
            Form(unhideToken, ("id", post.Id.ToString()))))
        {
            Assert.Equal(HttpStatusCode.Redirect, unhideResponse.StatusCode);
        }

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider
            .GetRequiredService<BlogDbContext>();
        var unhidden = await verifyContext.Posts
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == post.Id);
        Assert.False(unhidden.IsHidden);

        using var visibleAgain = await publicClient.GetAsync(
            $"/Posts/Details?slug={post.Slug}");
        Assert.Equal(HttpStatusCode.OK, visibleAgain.StatusCode);
    }

    [Fact]
    public async Task AdminCanToggleHiddenPostsInAllPostsList()
    {
        var author = await CreateUserAsync("Toggle Hidden Author");
        var admin = await CreateUserAsync("Toggle Hidden Admin", isAdmin: true);
        var post = await CreatePostAsync(author, "toggle-hidden", isHidden: true);

        using var adminClient = await LoginAsync(admin.Email!);
        var defaultHtml = await adminClient.GetStringAsync("/Posts");
        Assert.DoesNotContain(post.Title, defaultHtml);
        Assert.Contains("Show hidden", defaultHtml);

        var shownHtml = await adminClient.GetStringAsync("/Posts?showHidden=true");
        Assert.Contains(post.Title, shownHtml);
        Assert.Contains("id=\"show-hidden\"", shownHtml);
        Assert.Contains("checked", shownHtml);
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });

    private async Task<HttpClient> LoginAsync(string email)
    {
        var client = CreateClient();
        var token = await GetAntiforgeryTokenAsync(
            client,
            "/Identity/Account/Login");
        var response = await client.PostAsync(
            "/Identity/Account/Login",
            Form(
                token,
                ("Input.Email", email),
                ("Input.Password", Password),
                ("Input.RememberMe", "false")));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private async Task<ApplicationUser> CreateUserAsync(
        string displayName,
        bool isAdmin = false)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var created = new ApplicationUser
        {
            DisplayName = displayName,
            UserName = UniqueEmail("hidden"),
            EmailConfirmed = true
        };
        created.Email = created.UserName;
        Assert.True((await userManager.CreateAsync(created, Password)).Succeeded);

        if (isAdmin)
        {
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roleManager.RoleExistsAsync(RoleNames.Admin))
            {
                Assert.True(
                    (await roleManager.CreateAsync(
                        new IdentityRole(RoleNames.Admin))).Succeeded);
            }

            Assert.True(
                (await userManager.AddToRoleAsync(created, RoleNames.Admin))
                    .Succeeded);
        }

        return created;
    }

    private async Task<Post> CreatePostAsync(
        ApplicationUser author,
        string label,
        bool isHidden = false)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
        var slug = $"{label}-{Guid.NewGuid():N}";
        var post = new Post
        {
            AuthorId = author.Id,
            Title = $"{label} title",
            Slug = slug,
            Content = "Hidden post test content.",
            Category = PostCategory.LiteratureAndStuff,
            IsPublished = true,
            IsHidden = isHidden,
            CreatedAt = DateTime.UtcNow
        };
        context.Posts.Add(post);
        await context.SaveChangesAsync();
        return post;
    }

    private static async Task<string> GetAntiforgeryTokenAsync(
        HttpClient client,
        string path)
    {
        var html = await client.GetStringAsync(path);
        const string marker = "name=\"__RequestVerificationToken\"";
        var markerIndex = html.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0);

        var valueStart = html.IndexOf(
            "value=\"",
            markerIndex,
            StringComparison.Ordinal) + "value=\"".Length;
        var valueEnd = html.IndexOf('"', valueStart);
        return html[valueStart..valueEnd];
    }

    private static FormUrlEncodedContent Form(
        string antiForgeryToken,
        params (string Name, string Value)[] fields)
    {
        var values = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", antiForgeryToken)
        };
        values.AddRange(fields.Select(
            field => new KeyValuePair<string, string>(
                field.Name,
                field.Value)));
        return new FormUrlEncodedContent(values);
    }

    private static string UniqueEmail(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}@example.com";
}
