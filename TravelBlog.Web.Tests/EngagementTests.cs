using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TravelBlog.Web.Authorization;
using TravelBlog.Web.Data;
using TravelBlog.Web.Models;

namespace TravelBlog.Web.Tests;

[Collection(IntegrationTestCollection.Name)]
public sealed class EngagementTests
{
    private const string Password = "Test-pass1!";
    private readonly TravelBlogWebApplicationFactory _factory;

    public EngagementTests(TravelBlogWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ViewingPublishedPostRecordsUniqueViews()
    {
        var author = await CreateUserAsync("View Author");
        var post = await CreatePostAsync(author, "view-count");
        using var first = CreateClient();
        using var second = CreateClient();

        var firstVisit = await first.GetAsync(
            $"/Posts/Details?slug={post.Slug}");
        var firstRepeat = await first.GetAsync(
            $"/Posts/Details?slug={post.Slug}");
        var secondVisit = await second.GetAsync(
            $"/Posts/Details?slug={post.Slug}");

        Assert.Equal(HttpStatusCode.OK, firstVisit.StatusCode);
        Assert.Equal(HttpStatusCode.OK, firstRepeat.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondVisit.StatusCode);

        var firstHtml = await firstVisit.Content.ReadAsStringAsync();
        var repeatHtml = await firstRepeat.Content.ReadAsStringAsync();
        var secondHtml = await secondVisit.Content.ReadAsStringAsync();
        Assert.Contains("1 view", firstHtml);
        Assert.Contains("1 view", repeatHtml);
        Assert.Contains("2 views", secondHtml);

        await WithServicesAsync(async services =>
        {
            var count = await services
                .GetRequiredService<BlogDbContext>()
                .PostViews
                .CountAsync(view => view.PostId == post.Id);
            Assert.Equal(2, count);
        });
    }

    [Fact]
    public async Task UnpublishedPostViewByOwnerIsNotCounted()
    {
        var author = await CreateUserAsync("Draft View Author");
        var post = await CreatePostAsync(
            author,
            "draft-view",
            isPublished: false);
        using var client = await LoginAsync(author.Email!);

        var response = await client.GetAsync(
            $"/Posts/Details?slug={post.Slug}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("0 views", html);

        await WithServicesAsync(async services =>
        {
            Assert.False(await services
                .GetRequiredService<BlogDbContext>()
                .PostViews
                .AnyAsync(view => view.PostId == post.Id));
        });
    }

    [Fact]
    public async Task VerifiedUserCanLikeAndUnlikePost()
    {
        var author = await CreateUserAsync("Like Post Author");
        var liker = await CreateUserAsync("Post Liker");
        var post = await CreatePostAsync(author, "like-post");
        using var client = await LoginAsync(liker.Email!);
        var pagePath = $"/Posts/Details?slug={post.Slug}";
        var token = await GetAntiforgeryTokenAsync(client, pagePath);

        var like = await client.PostAsync(
            $"/Posts/ToggleLike?slug={post.Slug}",
            Form(token));
        Assert.Equal(HttpStatusCode.Redirect, like.StatusCode);

        await WithServicesAsync(async services =>
        {
            Assert.True(await services
                .GetRequiredService<BlogDbContext>()
                .PostLikes
                .AnyAsync(existing =>
                    existing.PostId == post.Id &&
                    existing.UserId == liker.Id));
        });

        var likedHtml = await client.GetStringAsync(pagePath);
        Assert.Contains("Unlike, 1 like", likedHtml);
        Assert.Contains("is-liked", likedHtml);

        var unlike = await client.PostAsync(
            $"/Posts/ToggleLike?slug={post.Slug}",
            Form(token));
        Assert.Equal(HttpStatusCode.Redirect, unlike.StatusCode);

        await WithServicesAsync(async services =>
        {
            Assert.False(await services
                .GetRequiredService<BlogDbContext>()
                .PostLikes
                .AnyAsync(existing => existing.PostId == post.Id));
        });
    }

    [Fact]
    public async Task AnonymousLikeRedirectsToLogin()
    {
        var author = await CreateUserAsync("Anon Like Author");
        var post = await CreatePostAsync(author, "anon-like");
        using var client = CreateClient();
        var token = await GetAntiforgeryTokenAsync(
            client,
            "/Identity/Account/Login");

        var response = await client.PostAsync(
            $"/Posts/ToggleLike?slug={post.Slug}",
            Form(token));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "/Identity/Account/Login",
            response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task UnverifiedUserCannotLikePost()
    {
        var author = await CreateUserAsync("Unverified Like Author");
        var unverified = await CreateUserAsync(
            "Unverified Liker",
            emailConfirmed: false);
        var post = await CreatePostAsync(author, "unverified-like");
        using var client = await LoginAsync(unverified.Email!);
        var token = await GetAntiforgeryTokenAsync(
            client,
            "/Identity/Account/Manage/Email");

        var response = await client.PostAsync(
            $"/Posts/ToggleLike?slug={post.Slug}",
            Form(token));
        AssertAccessDenied(response);
        await WithServicesAsync(async services =>
        {
            Assert.False(await services
                .GetRequiredService<BlogDbContext>()
                .PostLikes
                .AnyAsync(like => like.PostId == post.Id));
        });
    }

    [Fact]
    public async Task UserCanLikeComment()
    {
        var author = await CreateUserAsync("Comment Like Author");
        var commenter = await CreateUserAsync("Comment Like Writer");
        var liker = await CreateUserAsync("Comment Liker");
        var post = await CreatePostAsync(author, "like-comment");
        var comment = await CreateCommentAsync(
            post,
            commenter,
            "Please like this.");
        using var client = await LoginAsync(liker.Email!);
        var token = await GetAntiforgeryTokenAsync(
            client,
            $"/Posts/Details?slug={post.Slug}");

        var response = await client.PostAsync(
            $"/Posts/ToggleCommentLike/{comment.Id}",
            Form(token));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await WithServicesAsync(async services =>
        {
            Assert.True(await services
                .GetRequiredService<BlogDbContext>()
                .PostCommentLikes
                .AnyAsync(like =>
                    like.PostCommentId == comment.Id &&
                    like.UserId == liker.Id));
        });

        var html = await client.GetStringAsync(
            $"/Posts/Details?slug={post.Slug}");
        Assert.Contains($"ToggleCommentLike/{comment.Id}", html);
        Assert.Contains("Unlike, 1 like", html);
    }

    [Fact]
    public async Task MemberCanLikeDiscussionPost()
    {
        var admin = await CreateUserAsync("Discussion Like Admin", isAdmin: true);
        var member = await CreateUserAsync("Discussion Liker");
        var club = await CreateClubAsync(admin, "like-discussion");
        await AddMembershipAsync(club, member);
        var discussion = await CreateDiscussionAsync(
            club,
            admin,
            "Like this thread.");
        using var client = await LoginAsync(member.Email!);
        var token = await GetAntiforgeryTokenAsync(
            client,
            $"/BookClubs/{club.Slug}");

        var response = await client.PostAsync(
            $"/BookClubs/{club.Slug}/discussions/{discussion.Id}/Like",
            Form(token));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await WithServicesAsync(async services =>
        {
            Assert.True(await services
                .GetRequiredService<BlogDbContext>()
                .DiscussionPostLikes
                .AnyAsync(like =>
                    like.DiscussionPostId == discussion.Id &&
                    like.UserId == member.Id));
        });

        var html = await client.GetStringAsync($"/BookClubs/{club.Slug}");
        Assert.Contains("Unlike, 1 like", html);
    }

    [Fact]
    public async Task NonMemberCannotLikeDiscussionPost()
    {
        var admin = await CreateUserAsync("Discussion Gate Admin", isAdmin: true);
        var outsider = await CreateUserAsync("Discussion Outsider");
        var club = await CreateClubAsync(admin, "gate-discussion-like");
        var discussion = await CreateDiscussionAsync(
            club,
            admin,
            "Members only likes.");
        using var client = await LoginAsync(outsider.Email!);
        var token = await GetAntiforgeryTokenAsync(
            client,
            $"/BookClubs/{club.Slug}");

        var response = await client.PostAsync(
            $"/BookClubs/{club.Slug}/discussions/{discussion.Id}/Like",
            Form(token));
        AssertAccessDenied(response);
        await WithServicesAsync(async services =>
        {
            Assert.False(await services
                .GetRequiredService<BlogDbContext>()
                .DiscussionPostLikes
                .AnyAsync(like => like.DiscussionPostId == discussion.Id));
        });
    }

    [Fact]
    public async Task PostIndexShowsLikeAndViewCounts()
    {
        var author = await CreateUserAsync("Index Counts Author");
        var post = await CreatePostAsync(author, "index-counts");
        using var viewer = CreateClient();
        await viewer.GetAsync($"/Posts/Details?slug={post.Slug}");

        using var client = CreateClient();
        var html = await client.GetStringAsync("/Posts");
        Assert.Contains(post.Title, html);
        Assert.Contains("like-action", html);
        Assert.Contains("view", html);
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

    private async Task<ApplicationUser> CreateUserAsync(
        string displayName,
        bool emailConfirmed = true,
        bool isAdmin = false)
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
                EmailConfirmed = emailConfirmed
            };
            created.Email = created.UserName;
            Assert.True(
                (await userManager.CreateAsync(created, Password)).Succeeded);
            if (isAdmin)
            {
                var roleManager = services
                    .GetRequiredService<RoleManager<IdentityRole>>();
                if (!await roleManager.RoleExistsAsync(RoleNames.Admin))
                {
                    Assert.True((await roleManager.CreateAsync(
                        new IdentityRole(RoleNames.Admin))).Succeeded);
                }
                Assert.True((await userManager.AddToRoleAsync(
                    created,
                    RoleNames.Admin)).Succeeded);
            }
        });
        return created!;
    }

    private async Task<Post> CreatePostAsync(
        ApplicationUser author,
        string label,
        bool isPublished = true)
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
                Content = "Engagement integration test.",
                Category = PostCategory.LiteratureAndStuff,
                IsPublished = isPublished,
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

    private async Task<BookClub> CreateClubAsync(
        ApplicationUser admin,
        string label)
    {
        BookClub? created = null;
        await WithServicesAsync(async services =>
        {
            var context = services.GetRequiredService<BlogDbContext>();
            created = new BookClub
            {
                Name = $"{label} club",
                Slug = $"{label}-{Guid.NewGuid():N}",
                Description = "Engagement test club.",
                CreatedById = admin.Id,
                CreatedAt = DateTime.UtcNow,
                Memberships =
                [
                    new BookClubMembership
                    {
                        UserId = admin.Id,
                        JoinedAt = DateTime.UtcNow
                    }
                ]
            };
            context.BookClubs.Add(created);
            await context.SaveChangesAsync();
        });
        return created!;
    }

    private async Task AddMembershipAsync(BookClub club, ApplicationUser user)
    {
        await WithServicesAsync(async services =>
        {
            var context = services.GetRequiredService<BlogDbContext>();
            context.BookClubMemberships.Add(new BookClubMembership
            {
                ClubId = club.Id,
                UserId = user.Id,
                JoinedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        });
    }

    private async Task<DiscussionPost> CreateDiscussionAsync(
        BookClub club,
        ApplicationUser author,
        string body)
    {
        DiscussionPost? created = null;
        await WithServicesAsync(async services =>
        {
            var context = services.GetRequiredService<BlogDbContext>();
            created = new DiscussionPost
            {
                ClubId = club.Id,
                AuthorId = author.Id,
                Body = body,
                CreatedAt = DateTime.UtcNow
            };
            context.DiscussionPosts.Add(created);
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
