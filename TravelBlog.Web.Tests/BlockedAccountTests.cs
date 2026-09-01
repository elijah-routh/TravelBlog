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
public sealed class BlockedAccountTests
{
    private const string Password = "Test-pass1!";
    private readonly TravelBlogWebApplicationFactory _factory;

    public BlockedAccountTests(TravelBlogWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task BlockedUserCannotCreatePost()
    {
        var user = await CreateUserAsync("Blocked Creator", isBlocked: true);
        using var client = await LoginAsync(user.Email!);
        var token = await GetAntiforgeryTokenAsync(
            client,
            "/Identity/Account/Manage/Index");

        var response = await client.PostAsync(
            "/Posts/Create",
            Form(
                token,
                ("Title", "Blocked post"),
                ("Slug", $"blocked-{Guid.NewGuid():N}"),
                ("Content", "Should not save."),
                ("Category", "1"),
                ("IsPublished", "false")));

        AssertAccessDenied(response);
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
        Assert.False(await context.Posts.AnyAsync(post => post.Title == "Blocked post"));
    }

    [Fact]
    public async Task BlockedUserCannotCommentOnPost()
    {
        var author = await CreateUserAsync("Post Author");
        var blocked = await CreateUserAsync("Blocked Commenter", isBlocked: true);
        var post = await CreatePostAsync(author, "blocked-comment");

        using var client = await LoginAsync(blocked.Email!);
        var token = await GetAntiforgeryTokenAsync(
            client,
            "/Identity/Account/Manage/Index");
        var response = await client.PostAsync(
            $"/Posts/AddComment?slug={post.Slug}",
            Form(
                token,
                ("NewComment.Body", "Nope.")));

        AssertAccessDenied(response);
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
        Assert.False(await context.PostComments.AnyAsync(
            comment => comment.PostId == post.Id));
    }

    [Fact]
    public async Task AdminCanBlockAndUnblockUser()
    {
        var target = await CreateUserAsync("Block Target");
        var admin = await CreateUserAsync("Block Admin", isAdmin: true);

        using var adminClient = await LoginAsync(admin.Email!);
        var blockToken = await GetAntiforgeryTokenAsync(
            adminClient,
            "/Users");

        using (var blockResponse = await adminClient.PostAsync(
            "/Users/Block",
            Form(blockToken, ("id", target.Id))))
        {
            Assert.Equal(HttpStatusCode.Redirect, blockResponse.StatusCode);
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var blocked = await userManager.FindByIdAsync(target.Id);
        Assert.NotNull(blocked);
        Assert.True(blocked.IsBlocked);

        var unblockToken = await GetAntiforgeryTokenAsync(adminClient, "/Users");
        using (var unblockResponse = await adminClient.PostAsync(
            "/Users/Unblock",
            Form(unblockToken, ("id", target.Id))))
        {
            Assert.Equal(HttpStatusCode.Redirect, unblockResponse.StatusCode);
        }

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyUserManager = verifyScope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var unblocked = await verifyUserManager.FindByIdAsync(target.Id);
        Assert.NotNull(unblocked);
        Assert.False(unblocked.IsBlocked);
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
        bool isAdmin = false,
        bool isBlocked = false)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var created = new ApplicationUser
        {
            DisplayName = displayName,
            UserName = UniqueEmail("blocked"),
            EmailConfirmed = true,
            IsBlocked = isBlocked
        };
        created.Email = created.UserName;
        Assert.True((await userManager.CreateAsync(created, Password)).Succeeded);

        if (isBlocked)
        {
            created.IsBlocked = true;
            Assert.True((await userManager.UpdateAsync(created)).Succeeded);
        }

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

    private async Task<Post> CreatePostAsync(ApplicationUser author, string label)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
        var post = new Post
        {
            AuthorId = author.Id,
            Title = $"{label} title",
            Slug = $"{label}-{Guid.NewGuid():N}",
            Content = "Blocked account test content.",
            Category = PostCategory.LiteratureAndStuff,
            IsPublished = true,
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

    private static void AssertAccessDenied(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return;
        }

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "/Identity/Account/AccessDenied",
            response.Headers.Location?.AbsolutePath);
    }
}
