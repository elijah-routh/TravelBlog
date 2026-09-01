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
public sealed class SitePagesTests
{
    private const string Password = "Test-pass1!";
    private readonly TravelBlogWebApplicationFactory _factory;

    public SitePagesTests(TravelBlogWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FaqPageRendersQuestions()
    {
        using var client = CreateClient();
        var html = await client.GetStringAsync("/Home/Faq");

        Assert.Contains(">FAQs</h1>", html);
        Assert.Contains("What is a Lampoon?", html);
        Assert.Contains("Rock. Great Question!", html);
        Assert.Contains("Send us a message", html);
    }

    [Fact]
    public async Task ContactPageRequiresSignIn()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync("/Home/Contact");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "/Identity/Account/Login",
            response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task ContactFormCreatesAdminOnlyPost()
    {
        var user = await CreateUserAsync("Contact Sender");
        using var client = await LoginAsync(user.Email!);
        var token = await GetAntiforgeryTokenAsync(client, "/Home/Contact");

        using var response = await client.PostAsync(
            "/Home/Contact",
            Form(
                token,
                ("Title", "Question about clubs"),
                ("Content", "How do I start a new book club here?")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Thanks for reaching out", html);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
        var message = await context.Posts.SingleAsync(post =>
            post.Title == "Question about clubs");
        Assert.Equal(PostCategory.Contact, message.Category);
        Assert.False(message.IsPublished);

        using var anonymousClient = CreateClient();
        using var detailsResponse = await anonymousClient.GetAsync(
            $"/Posts/Details/{message.Slug}");
        Assert.Equal(HttpStatusCode.NotFound, detailsResponse.StatusCode);
    }

    [Fact]
    public async Task ContactPageShowsDailyLimitAfterFirstMessage()
    {
        var user = await CreateUserAsync("Daily Contact Sender");
        using var client = await LoginAsync(user.Email!);
        var firstToken = await GetAntiforgeryTokenAsync(client, "/Home/Contact");

        using (var firstResponse = await client.PostAsync(
            "/Home/Contact",
            Form(
                firstToken,
                ("Title", "First message"),
                ("Content", "This is my first contact message today."))))
        {
            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        }

        var html = await client.GetStringAsync("/Home/Contact");
        Assert.Contains("already sent a contact message today", html);
        Assert.DoesNotContain("name=\"Title\"", html);
    }

    [Fact]
    public async Task AdminNavListsContactMessages()
    {
        var sender = await CreateUserAsync("Contact Inbox Sender");
        using (var senderClient = await LoginAsync(sender.Email!))
        {
            var token = await GetAntiforgeryTokenAsync(senderClient, "/Home/Contact");
            using var response = await senderClient.PostAsync(
                "/Home/Contact",
                Form(
                    token,
                    ("Title", "Admin inbox test"),
                    ("Content", "Please review this contact message.")));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var admin = await CreateUserAsync("Contact Admin", isAdmin: true);
        using var adminClient = await LoginAsync(admin.Email!);
        var html = await adminClient.GetStringAsync("/Users/Contact");

        Assert.Contains("Contact Messages", html);
        Assert.Contains("Admin inbox test", html);
        Assert.Contains("admin-nav", html);
        Assert.Contains("href=\"/Users/Contact\"", html);
        Assert.Contains("href=\"/Users\"", html);
    }

    [Fact]
    public async Task LayoutIncludesFooterLinks()
    {
        using var client = CreateClient();
        var html = await client.GetStringAsync("/");

        Assert.Contains(">FAQ</a>", html);
        Assert.Contains(">Contact</a>", html);
        Assert.Contains(">Privacy</a>", html);
    }

    [Fact]
    public async Task NavbarHighlightsCurrentSection()
    {
        using var client = CreateClient();

        var homeHtml = await client.GetStringAsync("/");
        Assert.Matches(
            @"class=""nav-link is-active""[\s\S]*?aria-current=""page""[\s\S]*?>[\s\r\n]*Home",
            homeHtml);

        var postsHtml = await client.GetStringAsync("/Posts");
        Assert.Matches(
            @"class=""nav-link is-active""[\s\S]*?aria-current=""page""[\s\S]*?>[\s\r\n]*Posts",
            postsHtml);
        Assert.DoesNotMatch(
            @"class=""nav-link is-active""[\s\S]*?>[\s\r\n]*Home",
            postsHtml);

        var clubsHtml = await client.GetStringAsync("/BookClubs");
        Assert.Matches(
            @"class=""nav-link is-active""[\s\S]*?aria-current=""page""[\s\S]*?>[\s\r\n]*Book Clubs",
            clubsHtml);
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
        bool emailConfirmed = true)
    {
        ApplicationUser? created = null;
        await using var scope = _factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        created = new ApplicationUser
        {
            DisplayName = displayName,
            UserName = UniqueEmail("contact"),
            EmailConfirmed = emailConfirmed
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
