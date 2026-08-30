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
public sealed class AccountAnonymizationTests
{
    private const string Password = "Test-pass1!";
    private readonly TravelBlogWebApplicationFactory _factory;

    public AccountAnonymizationTests(
        TravelBlogWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OnlyBootstrapAdminCanOpenOrPostAccountRemoval()
    {
        var star = await EnsureBootstrapAdminAsync();
        var admin = await CreateUserAsync("Normal Admin", isAdmin: true);
        var target = await CreateUserAsync("Removal Target");
        using var adminClient = await LoginAsync(admin.Email!);
        var adminToken = await GetAntiforgeryTokenAsync(adminClient, "/Users");

        var getResponse = await adminClient.GetAsync(
            $"/Users/Remove/{target.Id}");
        var postResponse = await adminClient.PostAsync(
            "/Users/Remove",
            Form(adminToken, ("id", target.Id)));

        AssertAccessDenied(getResponse);
        AssertAccessDenied(postResponse);

        using var starClient = await LoginAsync(star.Email!);
        var usersHtml = await starClient.GetStringAsync("/Users");
        Assert.Contains("Admin (star)", usersHtml);
        Assert.Contains("Remove account", usersHtml);
        Assert.DoesNotContain(DeletedUserConstants.DisplayName, usersHtml);

        var adminHtml = await adminClient.GetStringAsync("/Users");
        Assert.DoesNotContain("Remove account", adminHtml);
    }

    [Fact]
    public async Task RemovalGuardsProtectedTargetsAndRequiresAntiforgery()
    {
        var star = await EnsureBootstrapAdminAsync();
        var target = await CreateUserAsync("Antiforgery Target");
        using var client = await LoginAsync(star.Email!);
        var token = await GetAntiforgeryTokenAsync(client, "/Users");

        AssertAccessDenied(await client.GetAsync(
            $"/Users/Remove/{BootstrapAdminConstants.UserId}"));
        AssertAccessDenied(await client.GetAsync(
            $"/Users/Remove/{DeletedUserConstants.UserId}"));
        AssertAccessDenied(await client.PostAsync(
            "/Users/Remove",
            Form(token, ("id", star.Id))));
        AssertAccessDenied(await client.PostAsync(
            "/Users/Remove",
            Form(token, ("id", DeletedUserConstants.UserId))));

        var noToken = await client.PostAsync(
            "/Users/Remove",
            new FormUrlEncodedContent(
            [
                new("id", target.Id)
            ]));
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
    }

    [Fact]
    public async Task RemovalAnonymizesContentAndDeletesIdentityData()
    {
        var star = await EnsureBootstrapAdminAsync();
        var target = await CreateUserAsync("Content Owner");
        var oldEmail = target.Email!;
        const string imageKey = "posts/preserved-image.jpg";
        int postId = 0;
        int commentId = 0;
        int discussionId = 0;
        int noticeId = 0;
        int clubId = 0;

        await WithServicesAsync(async services =>
        {
            var context = services.GetRequiredService<BlogDbContext>();
            var post = new Post
            {
                AuthorId = target.Id,
                Title = "Preserved post",
                Slug = $"preserved-{Guid.NewGuid():N}",
                Content = "Preserved content.",
                ImagePath = "https://images.test/preserved.jpg",
                ImageObjectKey = imageKey,
                Category = PostCategory.LiteratureAndStuff,
                IsPublished = true
            };
            var club = new BookClub
            {
                Name = "Preserved Club",
                Slug = $"preserved-club-{Guid.NewGuid():N}",
                CreatedById = target.Id
            };
            context.AddRange(post, club);
            await context.SaveChangesAsync();

            var comment = new PostComment
            {
                PostId = post.Id,
                AuthorId = target.Id,
                Body = "Preserved comment."
            };
            var notice = new ClubNotice
            {
                ClubId = club.Id,
                AuthorId = target.Id,
                Body = "Preserved notice."
            };
            var discussion = new DiscussionPost
            {
                ClubId = club.Id,
                AuthorId = target.Id,
                Body = "Preserved discussion."
            };
            context.AddRange(
                comment,
                notice,
                discussion,
                new BookClubMembership
                {
                    ClubId = club.Id,
                    UserId = target.Id
                });
            await context.SaveChangesAsync();

            var poll = new DiscussionPoll
            {
                DiscussionPostId = discussion.Id,
                Options =
                [
                    new DiscussionPollOption
                    {
                        Text = "Option one",
                        SortOrder = 0
                    },
                    new DiscussionPollOption
                    {
                        Text = "Option two",
                        SortOrder = 1
                    }
                ]
            };
            context.DiscussionPolls.Add(poll);
            await context.SaveChangesAsync();
            context.DiscussionPollVotes.Add(new DiscussionPollVote
            {
                PollId = poll.Id,
                OptionId = poll.Options.First().Id,
                UserId = target.Id
            });
            await context.SaveChangesAsync();

            postId = post.Id;
            commentId = comment.Id;
            discussionId = discussion.Id;
            noticeId = notice.Id;
            clubId = club.Id;
        });

        using var client = await LoginAsync(star.Email!);
        var token = await GetAntiforgeryTokenAsync(client, "/Users");
        var response = await client.PostAsync(
            "/Users/Remove",
            Form(token, ("id", target.Id)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await WithServicesAsync(async services =>
        {
            var context = services.GetRequiredService<BlogDbContext>();
            context.ChangeTracker.Clear();
            var manager =
                services.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.Null(await manager.FindByIdAsync(target.Id));
            Assert.Null(await manager.FindByEmailAsync(oldEmail));

            var sentinel = await context.Users.SingleAsync(user =>
                user.Id == DeletedUserConstants.UserId);
            Assert.Equal(DeletedUserConstants.DisplayName, sentinel.DisplayName);
            Assert.Null(sentinel.Email);
            Assert.Null(sentinel.UserName);
            Assert.Null(sentinel.PasswordHash);
            Assert.Empty(await manager.GetRolesAsync(sentinel));

            var storedPost = await context.Posts.SingleAsync(
                post => post.Id == postId);
            Assert.Equal(DeletedUserConstants.UserId, storedPost.AuthorId);
            Assert.Equal(imageKey, storedPost.ImageObjectKey);
            Assert.Equal(
                DeletedUserConstants.UserId,
                (await context.PostComments.SingleAsync(
                    comment => comment.Id == commentId)).AuthorId);
            Assert.Equal(
                DeletedUserConstants.UserId,
                (await context.DiscussionPosts.SingleAsync(
                    post => post.Id == discussionId)).AuthorId);
            Assert.Equal(
                DeletedUserConstants.UserId,
                (await context.ClubNotices.SingleAsync(
                    notice => notice.Id == noticeId)).AuthorId);
            Assert.Equal(
                DeletedUserConstants.UserId,
                (await context.BookClubs.SingleAsync(
                    club => club.Id == clubId)).CreatedById);
            Assert.False(await context.BookClubMemberships.AnyAsync(
                membership => membership.UserId == target.Id));
            Assert.False(await context.DiscussionPollVotes.AnyAsync(
                vote => vote.UserId == target.Id));

            var replacement = new ApplicationUser
            {
                DisplayName = "Replacement User",
                UserName = oldEmail,
                Email = oldEmail,
                EmailConfirmed = true
            };
            Assert.True(
                (await manager.CreateAsync(replacement, Password)).Succeeded);
        });
    }

    [Fact]
    public async Task UserCanCloseOwnAccountFromPersonalData()
    {
        var user = await CreateUserAsync("Self Close Owner");
        var oldEmail = user.Email!;
        int postId = 0;
        await WithServicesAsync(async services =>
        {
            var context = services.GetRequiredService<BlogDbContext>();
            var post = new Post
            {
                AuthorId = user.Id,
                Title = "Self close post",
                Slug = $"self-close-{Guid.NewGuid():N}",
                Content = "Self close content.",
                Category = PostCategory.LiteratureAndStuff,
                IsPublished = true
            };
            context.Posts.Add(post);
            await context.SaveChangesAsync();
            postId = post.Id;
        });

        using var client = await LoginAsync(oldEmail);
        var token = await GetAntiforgeryTokenAsync(
            client,
            "/Identity/Account/Manage/DeletePersonalData");
        var response = await client.PostAsync(
            "/Identity/Account/Manage/DeletePersonalData",
            Form(token, ("Input.Password", Password)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
        await WithServicesAsync(async services =>
        {
            var context = services.GetRequiredService<BlogDbContext>();
            context.ChangeTracker.Clear();
            var manager =
                services.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.Null(await manager.FindByIdAsync(user.Id));
            Assert.Equal(
                DeletedUserConstants.UserId,
                (await context.Posts.SingleAsync(
                    post => post.Id == postId)).AuthorId);
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

    private async Task<ApplicationUser> EnsureBootstrapAdminAsync()
    {
        ApplicationUser? star = null;
        await WithServicesAsync(async services =>
        {
            var manager =
                services.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager =
                services.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roleManager.RoleExistsAsync(RoleNames.Admin))
            {
                Assert.True((await roleManager.CreateAsync(
                    new IdentityRole(RoleNames.Admin))).Succeeded);
            }

            star = await manager.FindByIdAsync(BootstrapAdminConstants.UserId);
            if (star is null)
            {
                star = new ApplicationUser
                {
                    Id = BootstrapAdminConstants.UserId,
                    DisplayName = "Admin Star",
                    UserName = UniqueEmail("admin-star"),
                    EmailConfirmed = true
                };
                star.Email = star.UserName;
                Assert.True((await manager.CreateAsync(star, Password)).Succeeded);
            }
            else
            {
                if (await manager.HasPasswordAsync(star))
                {
                    Assert.True((await manager.RemovePasswordAsync(star)).Succeeded);
                }
                Assert.True((await manager.AddPasswordAsync(
                    star,
                    Password)).Succeeded);
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

    private async Task<ApplicationUser> CreateUserAsync(
        string displayName,
        bool isAdmin = false)
    {
        ApplicationUser? user = null;
        await WithServicesAsync(async services =>
        {
            var manager =
                services.GetRequiredService<UserManager<ApplicationUser>>();
            var email = UniqueEmail("account");
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
                var roleManager =
                    services.GetRequiredService<RoleManager<IdentityRole>>();
                if (!await roleManager.RoleExistsAsync(RoleNames.Admin))
                {
                    Assert.True((await roleManager.CreateAsync(
                        new IdentityRole(RoleNames.Admin))).Succeeded);
                }
                Assert.True((await manager.AddToRoleAsync(
                    user,
                    RoleNames.Admin)).Succeeded);
            }
        });
        return user!;
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
