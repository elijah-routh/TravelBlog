using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TravelBlog.Web.Authorization;
using TravelBlog.Web.Data;
using TravelBlog.Web.Models;

namespace TravelBlog.Web.Tests;

[Collection(IntegrationTestCollection.Name)]
public sealed class AuthorizationAndIdentityTests
{
    private const string Password = "Test-pass1!";
    private readonly TravelBlogWebApplicationFactory _factory;

    public AuthorizationAndIdentityTests(
        TravelBlogWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AnonymousCreateRedirectsToLogin()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/Posts/Create");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "/Identity/Account/Login",
            response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task RegistrationCapturesDisplayName()
    {
        using var client = CreateClient();
        var email = UniqueEmail("registered");
        var token = await GetAntiforgeryTokenAsync(
            client,
            "/Identity/Account/Register");

        var response = await client.PostAsync(
            "/Identity/Account/Register",
            Form(token,
                ("Input.DisplayName", "  Trail Writer  "),
                ("Input.Email", email),
                ("Input.Password", Password),
                ("Input.ConfirmPassword", Password)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await WithServicesAsync(async services =>
        {
            var user = await services
                .GetRequiredService<UserManager<ApplicationUser>>()
                .FindByEmailAsync(email);
            Assert.NotNull(user);
            Assert.Equal("Trail Writer", user.DisplayName);
        });
    }

    [Fact]
    public async Task TwoFactorUserIsPromptedForAuthenticatorCode()
    {
        var user = await CreateUserAsync("Two Factor Author");
        await WithServicesAsync(async services =>
        {
            var userManager =
                services.GetRequiredService<UserManager<ApplicationUser>>();
            var storedUser = await userManager.FindByIdAsync(user.Id);
            Assert.NotNull(storedUser);
            Assert.True(
                (await userManager.ResetAuthenticatorKeyAsync(storedUser)).Succeeded);
            Assert.True(
                (await userManager.SetTwoFactorEnabledAsync(
                    storedUser,
                    true)).Succeeded);
        });

        using var client = CreateClient();
        var loginToken = await GetAntiforgeryTokenAsync(
            client,
            "/Identity/Account/Login");
        var loginResponse = await client.PostAsync(
            "/Identity/Account/Login",
            Form(loginToken,
                ("Input.Email", user.Email!),
                ("Input.Password", Password),
                ("Input.RememberMe", "false")));

        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        var twoFactorLocation =
            loginResponse.Headers.Location?.OriginalString;
        Assert.NotNull(twoFactorLocation);
        Assert.StartsWith(
            "/Identity/Account/LoginWith2fa",
            twoFactorLocation);

        var twoFactorPage = await client.GetAsync(twoFactorLocation);
        Assert.Equal(HttpStatusCode.OK, twoFactorPage.StatusCode);
        var twoFactorHtml = await twoFactorPage.Content.ReadAsStringAsync();
        Assert.Contains("Input.TwoFactorCode", twoFactorHtml);
        Assert.Contains("Remember this browser", twoFactorHtml);
    }

    [Fact]
    public async Task CreationAlwaysUsesCurrentUserAsAuthor()
    {
        var author = await CreateUserAsync("Current Author");
        var other = await CreateUserAsync("Posted Owner");
        using var client = await LoginAsync(author.Email!);
        var token = await GetAntiforgeryTokenAsync(client, "/Posts/Create");
        var slug = $"owned-{Guid.NewGuid():N}";

        var response = await client.PostAsync(
            "/Posts/Create",
            Form(token,
                ("Title", "Ownership test"),
                ("Slug", slug),
                ("Content", "The authenticated user owns this."),
                ("Category", "1"),
                ("IsPublished", "true"),
                ("AuthorId", other.Id),
                ("Author.Id", other.Id)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await WithServicesAsync(async services =>
        {
            var post = await services
                .GetRequiredService<BlogDbContext>()
                .Posts.SingleAsync(post => post.Slug == slug);
            Assert.Equal(author.Id, post.AuthorId);
            Assert.NotEqual(other.Id, post.AuthorId);
        });
    }

    [Fact]
    public async Task RegularUserCannotEditOrDeleteAnotherUsersPost()
    {
        var owner = await CreateUserAsync("Protected Owner");
        var intruder = await CreateUserAsync("Intruder");
        var post = await CreatePostAsync(owner, "protected");
        using var client = await LoginAsync(intruder.Email!);
        var token = await GetAntiforgeryTokenAsync(client, "/Posts/Create");

        var edit = await client.PostAsync(
            $"/Posts/Edit/{post.Id}",
            Form(token,
                ("Id", post.Id.ToString()),
                ("Title", "Stolen"),
                ("Slug", post.Slug),
                ("Content", "Changed"),
                ("Category", "1")));
        var delete = await client.PostAsync(
            $"/Posts/Delete/{post.Id}",
            Form(token, ("id", post.Id.ToString())));

        AssertAccessDenied(edit);
        AssertAccessDenied(delete);
        await WithServicesAsync(async services =>
        {
            var stored = await services
                .GetRequiredService<BlogDbContext>()
                .Posts.SingleAsync(candidate => candidate.Id == post.Id);
            Assert.Equal(post.Title, stored.Title);
        });
    }

    [Fact]
    public async Task AdminCanAccessAnotherUsersEditAndDelete()
    {
        var owner = await CreateUserAsync("Admin Target");
        var admin = await CreateUserAsync("Access Admin", isAdmin: true);
        var post = await CreatePostAsync(owner, "admin-access");
        using var client = await LoginAsync(admin.Email!);

        var edit = await client.GetAsync($"/Posts/Edit/{post.Id}");
        var delete = await client.GetAsync($"/Posts/Delete/{post.Id}");

        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
    }

    [Fact]
    public async Task DraftDetailsAreRestrictedToOwnerAndAdmin()
    {
        var owner = await CreateUserAsync("Draft Owner");
        var unrelated = await CreateUserAsync("Draft Stranger");
        var admin = await CreateUserAsync("Draft Admin", isAdmin: true);
        var post = await CreatePostAsync(
            owner,
            "private-draft",
            isPublished: false);

        using var anonymousClient = CreateClient();
        var anonymous = await anonymousClient.GetAsync(
            $"/Posts/Details?slug={post.Slug}");
        using var unrelatedClient = await LoginAsync(unrelated.Email!);
        var forbidden = await unrelatedClient.GetAsync(
            $"/Posts/Details?slug={post.Slug}");
        using var ownerClient = await LoginAsync(owner.Email!);
        var ownerResponse = await ownerClient.GetAsync(
            $"/Posts/Details?slug={post.Slug}");
        using var adminClient = await LoginAsync(admin.Email!);
        var adminResponse = await adminClient.GetAsync(
            $"/Posts/Details?slug={post.Slug}");

        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.Equal(
            "/Identity/Account/Login",
            anonymous.Headers.Location?.AbsolutePath);
        AssertAccessDenied(forbidden);
        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
    }

    [Fact]
    public async Task PostsIndexFiltersRegularUsersButNotAdmins()
    {
        var first = await CreateUserAsync("Index First");
        var second = await CreateUserAsync("Index Second");
        var admin = await CreateUserAsync("Index Admin", isAdmin: true);
        var ownPost = await CreatePostAsync(first, "visible-own");
        var otherPost = await CreatePostAsync(second, "hidden-other");

        using var regularClient = await LoginAsync(first.Email!);
        var regularHtml = await regularClient.GetStringAsync("/Posts");
        using var adminClient = await LoginAsync(admin.Email!);
        var adminHtml = await adminClient.GetStringAsync("/Posts");

        Assert.Contains(ownPost.Title, regularHtml);
        Assert.DoesNotContain(otherPost.Title, regularHtml);
        Assert.Contains(ownPost.Title, adminHtml);
        Assert.Contains(otherPost.Title, adminHtml);
    }

    [Fact]
    public async Task FinalAdministratorCannotBeDemoted()
    {
        await RemoveAllAdminsAsync();
        var admin = await CreateUserAsync("Final Admin", isAdmin: true);
        using var client = await LoginAsync(admin.Email!);
        var token = await GetAntiforgeryTokenAsync(client, "/Users");

        var response = await client.PostAsync(
            "/Users/Demote",
            Form(token, ("id", admin.Id)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await WithServicesAsync(async services =>
        {
            var manager =
                services.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = await manager.FindByIdAsync(admin.Id);
            Assert.NotNull(stored);
            Assert.True(await manager.IsInRoleAsync(stored, RoleNames.Admin));
        });
    }

    [Fact]
    public async Task BootstrapInitializerConfiguresPlaceholderWithoutReplacingPassword()
    {
        const string originalPassword = "Original-pass1!";
        const string replacementPassword = "Replacement-pass1!";
        var email = UniqueEmail("bootstrap");

        await WithServicesAsync(async services =>
        {
            var userManager =
                services.GetRequiredService<UserManager<ApplicationUser>>();
            var existing = await userManager.FindByIdAsync(
                BootstrapAdminConstants.UserId);
            if (existing is not null)
            {
                await userManager.DeleteAsync(existing);
            }

            var placeholder = new ApplicationUser
            {
                Id = BootstrapAdminConstants.UserId,
                DisplayName = "Bootstrap Administrator",
                UserName = "bootstrap-admin@invalid.local",
                Email = "bootstrap-admin@invalid.local",
                EmailConfirmed = true
            };
            Assert.True((await userManager.CreateAsync(placeholder)).Succeeded);

            await BootstrapAdminInitializer.InitializeAsync(
                services,
                BootstrapConfiguration(
                    email,
                    originalPassword,
                    "Configured Admin"));

            var configured = await userManager.FindByIdAsync(
                BootstrapAdminConstants.UserId);
            Assert.NotNull(configured);
            Assert.Equal(email, configured.Email);
            Assert.Equal("Configured Admin", configured.DisplayName);
            Assert.True(await userManager.IsInRoleAsync(
                configured,
                RoleNames.Admin));
            Assert.True(await userManager.CheckPasswordAsync(
                configured,
                originalPassword));

            await BootstrapAdminInitializer.InitializeAsync(
                services,
                BootstrapConfiguration(
                    email,
                    replacementPassword,
                    "Renamed Admin"));

            Assert.True(await userManager.CheckPasswordAsync(
                configured,
                originalPassword));
            Assert.False(await userManager.CheckPasswordAsync(
                configured,
                replacementPassword));
            Assert.Equal("Renamed Admin", configured.DisplayName);
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

    private async Task<ApplicationUser> CreateUserAsync(
        string displayName,
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
                EmailConfirmed = true
            };
            created.Email = created.UserName;
            Assert.True(
                (await userManager.CreateAsync(created, Password)).Succeeded);

            if (isAdmin)
            {
                await EnsureAdminRoleAsync(services);
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
                Content = "Integration test content.",
                Category = PostCategory.LiteratureAndStuff,
                IsPublished = isPublished,
                CreatedAt = DateTime.UtcNow
            };
            context.Posts.Add(created);
            await context.SaveChangesAsync();
        });
        return created!;
    }

    private async Task RemoveAllAdminsAsync()
    {
        await WithServicesAsync(async services =>
        {
            await EnsureAdminRoleAsync(services);
            var userManager =
                services.GetRequiredService<UserManager<ApplicationUser>>();
            foreach (var admin in await userManager.GetUsersInRoleAsync(
                         RoleNames.Admin))
            {
                Assert.True((await userManager.RemoveFromRoleAsync(
                    admin,
                    RoleNames.Admin)).Succeeded);
            }
        });
    }

    private static async Task EnsureAdminRoleAsync(
        IServiceProvider services)
    {
        var roleManager =
            services.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync(RoleNames.Admin))
        {
            Assert.True((await roleManager.CreateAsync(
                new IdentityRole(RoleNames.Admin))).Succeeded);
        }
    }

    private async Task WithServicesAsync(
        Func<IServiceProvider, Task> action)
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

    private static IConfiguration BootstrapConfiguration(
        string email,
        string password,
        string displayName) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BootstrapAdmin:Email"] = email,
                ["BootstrapAdmin:Password"] = password,
                ["BootstrapAdmin:DisplayName"] = displayName
            })
            .Build();

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
