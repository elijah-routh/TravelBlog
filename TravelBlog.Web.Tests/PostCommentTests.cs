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
public sealed class PostCommentTests
{
    private const string Password = "Test-pass1!";
    private readonly TravelBlogWebApplicationFactory _factory;

    public PostCommentTests(TravelBlogWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AuthenticatedUserCanCommentOnPublishedPost()
    {
        var author = await CreateUserAsync("Comment Post Author");
        var commenter = await CreateUserAsync("Comment Writer");
        var post = await CreatePostAsync(author, "commentable");
        using var client = await LoginAsync(commenter.Email!);
        var token = await GetAntiforgeryTokenAsync(
            client,
            $"/Posts/Details?slug={post.Slug}");

        var response = await client.PostAsync(
            $"/Posts/AddComment?slug={post.Slug}",
            Form(token, ("NewComment.Body", "Loved this piece.")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await WithServicesAsync(async services =>
        {
            var comment = await services
                .GetRequiredService<BlogDbContext>()
                .PostComments
                .SingleAsync(existing => existing.PostId == post.Id);
            Assert.Equal(commenter.Id, comment.AuthorId);
            Assert.Equal("Loved this piece.", comment.Body);
        });
    }

    [Fact]
    public async Task AnonymousCommentRedirectsToLogin()
    {
        var author = await CreateUserAsync("Anon Comment Author");
        var post = await CreatePostAsync(author, "anon-comment");
        using var client = CreateClient();
        var token = await GetAntiforgeryTokenAsync(
            client,
            "/Identity/Account/Login");

        var response = await client.PostAsync(
            $"/Posts/AddComment?slug={post.Slug}",
            Form(token, ("NewComment.Body", "No login.")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "/Identity/Account/Login",
            response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task AuthorCanEditAndDeleteOwnComment()
    {
        var author = await CreateUserAsync("Own Comment Author");
        var commenter = await CreateUserAsync("Own Commenter");
        var post = await CreatePostAsync(author, "own-comment");
        var comment = await CreateCommentAsync(
            post,
            commenter,
            "Original comment.");
        using var client = await LoginAsync(commenter.Email!);
        var editToken = await GetAntiforgeryTokenAsync(
            client,
            $"/Posts/EditComment/{comment.Id}");

        var edit = await client.PostAsync(
            $"/Posts/EditComment/{comment.Id}",
            Form(editToken, ("Body", "Edited comment.")));
        Assert.Equal(HttpStatusCode.Redirect, edit.StatusCode);

        var deleteToken = await GetAntiforgeryTokenAsync(
            client,
            $"/Posts/DeleteComment/{comment.Id}");
        var delete = await client.PostAsync(
            $"/Posts/DeleteComment/{comment.Id}",
            Form(deleteToken));
        Assert.Equal(HttpStatusCode.Redirect, delete.StatusCode);

        await WithServicesAsync(async services =>
        {
            Assert.False(await services
                .GetRequiredService<BlogDbContext>()
                .PostComments
                .AnyAsync(existing => existing.Id == comment.Id));
        });
    }

    [Fact]
    public async Task OtherUserCannotEditOrDeleteComment()
    {
        var author = await CreateUserAsync("Protected Comment Author");
        var commenter = await CreateUserAsync("Protected Commenter");
        var stranger = await CreateUserAsync("Comment Stranger");
        var post = await CreatePostAsync(author, "protected-comment");
        var comment = await CreateCommentAsync(
            post,
            commenter,
            "Hands off.");
        using var client = await LoginAsync(stranger.Email!);
        var token = await GetAntiforgeryTokenAsync(
            client,
            $"/Posts/Details?slug={post.Slug}");

        var edit = await client.PostAsync(
            $"/Posts/EditComment/{comment.Id}",
            Form(token, ("Body", "Stolen comment.")));
        var delete = await client.PostAsync(
            $"/Posts/DeleteComment/{comment.Id}",
            Form(token));

        AssertAccessDenied(edit);
        AssertAccessDenied(delete);
        await WithServicesAsync(async services =>
        {
            var stored = await services
                .GetRequiredService<BlogDbContext>()
                .PostComments
                .SingleAsync(existing => existing.Id == comment.Id);
            Assert.Equal("Hands off.", stored.Body);
        });
    }

    [Fact]
    public async Task UserCanReplyToCommentButNotToAReply()
    {
        var author = await CreateUserAsync("Reply Post Author");
        var commenter = await CreateUserAsync("Reply Commenter");
        var replier = await CreateUserAsync("Comment Replier");
        var post = await CreatePostAsync(author, "comment-replies");
        var parent = await CreateCommentAsync(
            post,
            commenter,
            "Top-level comment.");
        using var client = await LoginAsync(replier.Email!);
        var pagePath = $"/Posts/Details?slug={post.Slug}";
        var token = await GetAntiforgeryTokenAsync(client, pagePath);

        var pageHtml = await client.GetStringAsync(pagePath);
        Assert.Contains(
            $"data-bs-target=\"#comment-reply-{parent.Id}\"",
            pageHtml);
        Assert.Contains(
            $"class=\"collapse\" id=\"comment-reply-{parent.Id}\"",
            pageHtml);

        var response = await client.PostAsync(
            $"/Posts/ReplyComment/{parent.Id}",
            Form(token, ("Body", "First-level reply.")));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        PostComment? reply = null;
        await WithServicesAsync(async services =>
        {
            reply = await services
                .GetRequiredService<BlogDbContext>()
                .PostComments
                .SingleAsync(comment =>
                    comment.ParentId == parent.Id &&
                    comment.Body == "First-level reply.");
        });
        Assert.NotNull(reply);

        var threadedHtml = await client.GetStringAsync(pagePath);
        Assert.Contains(
            $"data-bs-target=\"#comment-thread-{parent.Id}\"",
            threadedHtml);
        Assert.Contains(
            $"class=\"collapse\" id=\"comment-thread-{parent.Id}\"",
            threadedHtml);

        var nested = await client.PostAsync(
            $"/Posts/ReplyComment/{reply.Id}",
            Form(token, ("Body", "Nested reply.")));
        Assert.Equal(HttpStatusCode.BadRequest, nested.StatusCode);
        await WithServicesAsync(async services =>
        {
            Assert.False(await services
                .GetRequiredService<BlogDbContext>()
                .PostComments
                .AnyAsync(comment => comment.ParentId == reply.Id));
        });
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
            Form(token,
                ("Input.Email", email),
                ("Input.Password", Password),
                ("Input.RememberMe", "false")));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
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

    private async Task<Post> CreatePostAsync(ApplicationUser author, string label)
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
                Content = "Comment integration test.",
                Category = PostCategory.LiteratureAndStuff,
                IsPublished = true,
                CreatedAt = DateTime.UtcNow
            };
            context.Posts.Add(created);
            await context.SaveChangesAsync();
        });
        return created!;
    }

    private async Task<PostComment> CreateCommentAsync(
        Post post,
        ApplicationUser author,
        string body)
    {
        PostComment? created = null;
        await WithServicesAsync(async services =>
        {
            var context = services.GetRequiredService<BlogDbContext>();
            created = new PostComment
            {
                PostId = post.Id,
                AuthorId = author.Id,
                Body = body,
                CreatedAt = DateTime.UtcNow
            };
            context.PostComments.Add(created);
            await context.SaveChangesAsync();
        });
        return created!;
    }

    private async Task WithServicesAsync(Func<IServiceProvider, Task> action)
    {
        using var scope = _factory.Services.CreateScope();
        await action(scope.ServiceProvider);
    }

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

    private static string UniqueEmail(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}@example.test";
}
