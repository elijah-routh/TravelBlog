using System.Net;
using System.Net.Http.Headers;
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
public sealed class LockoutAndRateLimitTests
{
    private const string Password = "Test-pass1!";
    private static readonly byte[] PngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01];
    private readonly TravelBlogWebApplicationFactory _factory;

    public LockoutAndRateLimitTests(
        TravelBlogWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FifthPasswordFailureLocksAccountAndBlocksCorrectPassword()
    {
        var user = await CreateUserAsync(_factory, "Lockout User");
        using var client = CreateClient(_factory);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var response = await LoginPostAsync(
                client,
                user.Email!,
                "Wrong-pass1!");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            if (attempt < 5)
            {
                Assert.Contains("Invalid login attempt.", html);
            }
            else
            {
                Assert.Contains("temporarily locked", html);
            }
        }

        var correctPassword = await LoginPostAsync(
            client,
            user.Email!,
            Password);
        Assert.Equal(HttpStatusCode.OK, correctPassword.StatusCode);
        Assert.Contains(
            "temporarily locked",
            await correctPassword.Content.ReadAsStringAsync());
        await WithServicesAsync(_factory, async services =>
        {
            var manager =
                services.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = await manager.FindByIdAsync(user.Id);
            Assert.NotNull(stored);
            Assert.True(await manager.IsLockedOutAsync(stored));
        });
    }

    [Fact]
    public async Task OnlyAdminStarCanUnlockAndRestoreLogin()
    {
        var star = await EnsureBootstrapAdminAsync();
        var normalAdmin = await CreateUserAsync(
            _factory,
            "Normal Unlock Admin",
            isAdmin: true);
        var target = await CreateUserAsync(_factory, "Locked Target");
        await WithServicesAsync(_factory, async services =>
        {
            var manager =
                services.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = await manager.FindByIdAsync(target.Id);
            Assert.NotNull(stored);
            Assert.True((await manager.SetLockoutEndDateAsync(
                stored,
                DateTimeOffset.UtcNow.AddMinutes(15))).Succeeded);
            Assert.True((await manager.AccessFailedAsync(stored)).Succeeded);
        });

        using var adminClient = await LoginAsync(
            _factory,
            normalAdmin.Email!);
        var adminHtml = await adminClient.GetStringAsync("/Users");
        Assert.Contains("aria-label=\"Account locked\"", adminHtml);
        Assert.DoesNotContain(
            "aria-label=\"Unlock Locked Target",
            adminHtml);
        var adminToken = await GetAntiforgeryTokenAsync(adminClient, "/Users");
        var forbidden = await adminClient.PostAsync(
            "/Users/Unlock",
            Form(adminToken, ("id", target.Id)));
        AssertAccessDenied(forbidden);

        using var starClient = await LoginAsync(_factory, star.Email!);
        var starHtml = await starClient.GetStringAsync("/Users");
        Assert.Contains(
            "aria-label=\"Unlock Locked Target",
            starHtml);
        var starToken = await GetAntiforgeryTokenAsync(starClient, "/Users");
        var unlocked = await starClient.PostAsync(
            "/Users/Unlock",
            Form(starToken, ("id", target.Id)));
        Assert.Equal(HttpStatusCode.Redirect, unlocked.StatusCode);

        await WithServicesAsync(_factory, async services =>
        {
            var manager =
                services.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = await manager.FindByIdAsync(target.Id);
            Assert.NotNull(stored);
            Assert.False(await manager.IsLockedOutAsync(stored));
            Assert.Equal(0, await manager.GetAccessFailedCountAsync(stored));
        });
        using var targetClient = CreateClient(_factory);
        Assert.Equal(
            HttpStatusCode.Redirect,
            (await LoginPostAsync(
                targetClient,
                target.Email!,
                Password)).StatusCode);
    }

    [Fact]
    public async Task LoginIpLimiterRejectsSixteenthPostButNotGets()
    {
        using var factory = new TravelBlogWebApplicationFactory(
            useProductionContentRateLimits: true);
        using var client = CreateClient(factory);
        var token = await GetAntiforgeryTokenAsync(
            client,
            "/Identity/Account/Login");

        for (var attempt = 0; attempt < 15; attempt++)
        {
            using var response = await client.PostAsync(
                "/Identity/Account/Login",
                Form(token,
                    ("Input.Email", UniqueEmail("missing")),
                    ("Input.Password", "Wrong-pass1!"),
                    ("Input.RememberMe", "false")));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(
                HttpStatusCode.OK,
                (await client.GetAsync("/Identity/Account/Login")).StatusCode);
        }

        var rejected = await client.PostAsync(
            "/Identity/Account/Login",
            Form(token,
                ("Input.Email", UniqueEmail("missing-last")),
                ("Input.Password", "Wrong-pass1!"),
                ("Input.RememberMe", "false")));
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Contains(
            "Too many requests",
            await rejected.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CommentLimiterRejectsEleventhSubmissionPerUser()
    {
        using var factory = new TravelBlogWebApplicationFactory(
            useProductionContentRateLimits: true);
        var user = await CreateUserAsync(factory, "Limited Commenter");
        var post = await CreatePostAsync(factory, user);
        using var client = await LoginAsync(factory, user.Email!);
        var token = await GetAntiforgeryTokenAsync(
            client,
            $"/Posts/Details?slug={post.Slug}");

        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var response = await client.PostAsync(
                $"/Posts/AddComment?slug={post.Slug}",
                Form(token, ("NewComment.Body", $"Comment {attempt}")));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        var rejected = await client.PostAsync(
            $"/Posts/AddComment?slug={post.Slug}",
            Form(token, ("NewComment.Body", "One too many")));
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        await WithServicesAsync(factory, async services =>
        {
            Assert.Equal(
                10,
                await services.GetRequiredService<BlogDbContext>()
                    .PostComments.CountAsync(comment =>
                        comment.PostId == post.Id));
        });
    }

    [Fact]
    public async Task ImageLimiterCountsOnlyValidatedFileUploads()
    {
        using var factory = new TravelBlogWebApplicationFactory(
            useProductionContentRateLimits: true);
        var user = await CreateUserAsync(factory, "Limited Uploader");
        var editable = await CreatePostAsync(factory, user);
        using var client = await LoginAsync(factory, user.Email!);

        for (var edit = 0; edit < 7; edit++)
        {
            var token = await GetAntiforgeryTokenAsync(
                client,
                $"/Posts/Edit/{editable.Id}");
            using var noFileForm = new MultipartFormDataContent();
            AddFields(
                noFileForm,
                token,
                ("Id", editable.Id.ToString()),
                ("Title", $"Text edit {edit}"),
                ("Slug", editable.Slug),
                ("Content", "Text-only update."),
                ("Category", "1"),
                ("IsPublished", "true"));
            using var response = await client.PostAsync(
                $"/Posts/Edit/{editable.Id}",
                noFileForm);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        for (var upload = 0; upload < 5; upload++)
        {
            var token = await GetAntiforgeryTokenAsync(
                client,
                "/Posts/Create");
            using var form = ImageCreateForm(
                token,
                $"limited-upload-{Guid.NewGuid():N}");
            using var response = await client.PostAsync("/Posts/Create", form);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        var finalToken = await GetAntiforgeryTokenAsync(
            client,
            "/Posts/Create");
        var rejectedSlug = $"limited-upload-{Guid.NewGuid():N}";
        using var rejectedForm = ImageCreateForm(finalToken, rejectedSlug);
        var rejected = await client.PostAsync("/Posts/Create", rejectedForm);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal(5, factory.ImageStorage.Uploads.Count);
        await WithServicesAsync(factory, async services =>
        {
            Assert.False(await services.GetRequiredService<BlogDbContext>()
                .Posts.AnyAsync(post => post.Slug == rejectedSlug));
        });
    }

    private async Task<ApplicationUser> EnsureBootstrapAdminAsync()
    {
        ApplicationUser? star = null;
        await WithServicesAsync(_factory, async services =>
        {
            var manager =
                services.GetRequiredService<UserManager<ApplicationUser>>();
            await EnsureAdminRoleAsync(services);
            star = await manager.FindByIdAsync(BootstrapAdminConstants.UserId);
            if (star is null)
            {
                var email = UniqueEmail("unlock-star");
                star = new ApplicationUser
                {
                    Id = BootstrapAdminConstants.UserId,
                    DisplayName = "Unlock Star",
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true
                };
                Assert.True(
                    (await manager.CreateAsync(star, Password)).Succeeded);
            }
            else
            {
                if (await manager.HasPasswordAsync(star))
                {
                    Assert.True(
                        (await manager.RemovePasswordAsync(star)).Succeeded);
                }
                Assert.True(
                    (await manager.AddPasswordAsync(star, Password)).Succeeded);
                star.EmailConfirmed = true;
                Assert.True((await manager.UpdateAsync(star)).Succeeded);
            }
            if (!await manager.IsInRoleAsync(star, RoleNames.Admin))
            {
                Assert.True((await manager.AddToRoleAsync(
                    star,
                    RoleNames.Admin)).Succeeded);
            }
        });
        return star!;
    }

    private static async Task<ApplicationUser> CreateUserAsync(
        TravelBlogWebApplicationFactory factory,
        string displayName,
        bool isAdmin = false)
    {
        ApplicationUser? user = null;
        await WithServicesAsync(factory, async services =>
        {
            var manager =
                services.GetRequiredService<UserManager<ApplicationUser>>();
            var email = UniqueEmail("limits");
            user = new ApplicationUser
            {
                DisplayName = displayName,
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };
            Assert.True((await manager.CreateAsync(user, Password)).Succeeded);
            if (isAdmin)
            {
                await EnsureAdminRoleAsync(services);
                Assert.True((await manager.AddToRoleAsync(
                    user,
                    RoleNames.Admin)).Succeeded);
            }
        });
        return user!;
    }

    private static async Task<Post> CreatePostAsync(
        TravelBlogWebApplicationFactory factory,
        ApplicationUser user)
    {
        Post? post = null;
        await WithServicesAsync(factory, async services =>
        {
            post = new Post
            {
                AuthorId = user.Id,
                Title = "Rate limit post",
                Slug = $"rate-limit-{Guid.NewGuid():N}",
                Content = "Rate limit content.",
                Category = PostCategory.LiteratureAndStuff,
                IsPublished = true
            };
            var context = services.GetRequiredService<BlogDbContext>();
            context.Posts.Add(post);
            await context.SaveChangesAsync();
        });
        return post!;
    }

    private static async Task EnsureAdminRoleAsync(IServiceProvider services)
    {
        var roleManager =
            services.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync(RoleNames.Admin))
        {
            Assert.True((await roleManager.CreateAsync(
                new IdentityRole(RoleNames.Admin))).Succeeded);
        }
    }

    private static HttpClient CreateClient(
        TravelBlogWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });

    private static async Task<HttpClient> LoginAsync(
        TravelBlogWebApplicationFactory factory,
        string email)
    {
        var client = CreateClient(factory);
        var response = await LoginPostAsync(client, email, Password);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private static async Task<HttpResponseMessage> LoginPostAsync(
        HttpClient client,
        string email,
        string password)
    {
        var token = await GetAntiforgeryTokenAsync(
            client,
            "/Identity/Account/Login");
        return await client.PostAsync(
            "/Identity/Account/Login",
            Form(token,
                ("Input.Email", email),
                ("Input.Password", password),
                ("Input.RememberMe", "false")));
    }

    private static MultipartFormDataContent ImageCreateForm(
        string token,
        string slug)
    {
        var form = new MultipartFormDataContent();
        AddFields(
            form,
            token,
            ("Title", "Limited image"),
            ("Slug", slug),
            ("Content", "Image content."),
            ("Category", "1"),
            ("IsPublished", "true"));
        var file = new ByteArrayContent(PngBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "FeaturedImage", "featured.png");
        return form;
    }

    private static void AddFields(
        MultipartFormDataContent form,
        string token,
        params (string Key, string Value)[] fields)
    {
        foreach (var (key, value) in fields.Append(
                     ("__RequestVerificationToken", token)))
        {
            form.Add(new StringContent(value), key);
        }
    }

    private static async Task WithServicesAsync(
        TravelBlogWebApplicationFactory factory,
        Func<IServiceProvider, Task> action)
    {
        using var scope = factory.Services.CreateScope();
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
