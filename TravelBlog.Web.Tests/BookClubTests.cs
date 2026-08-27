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
public sealed class BookClubTests
{
    private const string Password = "Test-pass1!";
    private static readonly byte[] PngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01];
    private readonly TravelBlogWebApplicationFactory _factory;

    public BookClubTests(TravelBlogWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AnonymousCanViewClubIndexAndDetails()
    {
        var admin = await CreateUserAsync("Club Public Admin", isAdmin: true);
        var club = await CreateClubAsync(admin, "public-club");
        var book = await CreateBookAsync(club, "Visible Book", daysFromToday: 0);
        using var client = CreateClient();

        var index = await client.GetAsync("/BookClubs");
        var details = await client.GetAsync($"/BookClubs/{club.Slug}");
        var clubTimeline = await client.GetAsync(
            $"/BookClubs/{club.Slug}?view=timeline");
        var bookPage = await client.GetAsync(
            $"/BookClubs/{club.Slug}/books/{book.Id}");
        var timeline = await client.GetAsync("/BookClubs/Timeline");

        Assert.Equal(HttpStatusCode.OK, index.StatusCode);
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        Assert.Equal(HttpStatusCode.OK, clubTimeline.StatusCode);
        Assert.Equal(HttpStatusCode.OK, bookPage.StatusCode);
        Assert.Equal(HttpStatusCode.OK, timeline.StatusCode);

        var indexHtml = await index.Content.ReadAsStringAsync();
        var detailsHtml = await details.Content.ReadAsStringAsync();
        var clubTimelineHtml = await clubTimeline.Content.ReadAsStringAsync();
        var bookHtml = await bookPage.Content.ReadAsStringAsync();
        var timelineHtml = await timeline.Content.ReadAsStringAsync();
        Assert.Contains(club.Name, indexHtml);
        Assert.Contains("Book Clubs", indexHtml);
        Assert.Contains(club.Name, detailsHtml);
        Assert.Contains(book.Title, detailsHtml);
        Assert.Contains("View Full Timeline", detailsHtml);
        Assert.Contains("Reading timeline", clubTimelineHtml);
        Assert.Contains("Show Current Book", clubTimelineHtml);
        Assert.Contains(book.Title, bookHtml);
        Assert.Contains(book.ImagePath!, indexHtml);
        Assert.Contains(
            book.EndDate.ToString("MMM d, yyyy"),
            indexHtml);
        Assert.Contains("Combined Reading Timeline", timelineHtml);
        Assert.Contains(club.Name, timelineHtml);
        Assert.Contains(book.Title, timelineHtml);
    }

    [Fact]
    public async Task AnonymousDiscussionPostRedirectsToLogin()
    {
        var admin = await CreateUserAsync("Club Anon Admin", isAdmin: true);
        var club = await CreateClubAsync(admin, "anon-discussion");
        using var client = CreateClient();
        var token = await GetAntiforgeryTokenAsync(
            client,
            "/Identity/Account/Login");

        var response = await client.PostAsync(
            $"/BookClubs/{club.Slug}/Discussions",
            Form(token, ("NewDiscussion.Body", "Hello from nowhere.")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "/Identity/Account/Login",
            response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task NonMemberCannotPostDiscussion()
    {
        var admin = await CreateUserAsync("Club Gate Admin", isAdmin: true);
        var member = await CreateUserAsync("Club Outsider");
        var club = await CreateClubAsync(admin, "members-only-post");
        var book = await CreateBookAsync(club, "Closed Book", daysFromToday: 0);
        using var client = await LoginAsync(member.Email!);
        var token = await GetAntiforgeryTokenAsync(
            client,
            $"/BookClubs/{club.Slug}");

        var clubPost = await client.PostAsync(
            $"/BookClubs/{club.Slug}/Discussions",
            Form(token, ("NewDiscussion.Body", "I am not a member.")));
        var bookPost = await client.PostAsync(
            $"/BookClubs/{club.Slug}/books/{book.Id}/Discussions",
            Form(token, ("NewDiscussion.Body", "Still not a member.")));

        AssertAccessDenied(clubPost);
        AssertAccessDenied(bookPost);
        await WithServicesAsync(async services =>
        {
            var count = await services
                .GetRequiredService<BlogDbContext>()
                .DiscussionPosts
                .CountAsync(post => post.ClubId == club.Id);
            Assert.Equal(0, count);
        });
    }

    [Fact]
    public async Task MemberCanPostClubAndBookDiscussions()
    {
        var admin = await CreateUserAsync("Club Host Admin", isAdmin: true);
        var member = await CreateUserAsync("Club Member");
        var club = await CreateClubAsync(admin, "member-talk");
        var book = await CreateBookAsync(club, "Talk Book", daysFromToday: -2);
        await AddMembershipAsync(club, member);
        using var client = await LoginAsync(member.Email!);
        var clubToken = await GetAntiforgeryTokenAsync(
            client,
            $"/BookClubs/{club.Slug}");
        var bookToken = await GetAntiforgeryTokenAsync(
            client,
            $"/BookClubs/{club.Slug}/books/{book.Id}");

        var clubPost = await client.PostAsync(
            $"/BookClubs/{club.Slug}/Discussions",
            Form(clubToken, ("NewDiscussion.Body", "Club thread message.")));
        var bookPost = await client.PostAsync(
            $"/BookClubs/{club.Slug}/books/{book.Id}/Discussions",
            Form(bookToken, ("NewDiscussion.Body", "Book thread message.")));

        Assert.Equal(HttpStatusCode.Redirect, clubPost.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, bookPost.StatusCode);
        await WithServicesAsync(async services =>
        {
            var posts = await services
                .GetRequiredService<BlogDbContext>()
                .DiscussionPosts
                .Where(post => post.ClubId == club.Id)
                .ToListAsync();
            Assert.Contains(
                posts,
                post => post.ClubBookId is null &&
                    post.Body == "Club thread message." &&
                    post.AuthorId == member.Id);
            Assert.Contains(
                posts,
                post => post.ClubBookId == book.Id &&
                    post.Body == "Book thread message." &&
                    post.AuthorId == member.Id);
        });
    }

    [Fact]
    public async Task AdminCanCreateChapterThreadAndMemberCanPostInIt()
    {
        var admin = await CreateUserAsync("Thread Admin", isAdmin: true);
        var member = await CreateUserAsync("Thread Member");
        var club = await CreateClubAsync(admin, "chapter-threads");
        var book = await CreateBookAsync(club, "Chapter Book", daysFromToday: 0);
        await AddMembershipAsync(club, member);

        using var adminClient = await LoginAsync(admin.Email!);
        var createToken = await GetAntiforgeryTokenAsync(
            adminClient,
            $"/BookClubs/{club.Slug}/books/{book.Id}");
        var create = await adminClient.PostAsync(
            $"/BookClubs/{club.Slug}/books/{book.Id}/Threads",
            Form(createToken, ("NewThread.Title", "Chapters 1–3")));
        Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);

        BookDiscussionThread thread = null!;
        await WithServicesAsync(async services =>
        {
            thread = await services
                .GetRequiredService<BlogDbContext>()
                .BookDiscussionThreads
                .SingleAsync(existing =>
                    existing.ClubBookId == book.Id &&
                    existing.Title == "Chapters 1–3");
        });

        using var memberClient = await LoginAsync(member.Email!);
        var postToken = await GetAntiforgeryTokenAsync(
            memberClient,
            $"/BookClubs/{club.Slug}/books/{book.Id}?thread={thread.Id}");
        var post = await memberClient.PostAsync(
            $"/BookClubs/{club.Slug}/books/{book.Id}/Discussions?threadId={thread.Id}",
            Form(postToken, ("NewDiscussion.Body", "A chapter-specific thought.")));
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);

        var page = await memberClient.GetAsync(
            $"/BookClubs/{club.Slug}/books/{book.Id}?thread={thread.Id}");
        var html = await page.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("General", html);
        Assert.Contains("Chapters 1", html);
        Assert.Contains("A chapter-specific thought.", html);

        await WithServicesAsync(async services =>
        {
            Assert.True(await services
                .GetRequiredService<BlogDbContext>()
                .DiscussionPosts
                .AnyAsync(existing =>
                    existing.BookDiscussionThreadId == thread.Id &&
                    existing.Body == "A chapter-specific thought."));
        });
    }

    [Fact]
    public async Task NonAdminCannotCreateClubOrAddBookOrNotice()
    {
        var admin = await CreateUserAsync("Club Owner Admin", isAdmin: true);
        var user = await CreateUserAsync("Club Regular");
        var club = await CreateClubAsync(admin, "no-admin-tools");
        using var client = await LoginAsync(user.Email!);
        var token = await GetAntiforgeryTokenAsync(
            client,
            $"/BookClubs/{club.Slug}");

        var create = await client.PostAsync(
            "/BookClubs/Create",
            Form(token,
                ("Name", "Unauthorized Club"),
                ("Slug", $"nope-{Guid.NewGuid():N}")));
        var book = await client.PostAsync(
            $"/BookClubs/{club.Slug}/books/Create",
            Form(token,
                ("Title", "Unauthorized Book"),
                ("AuthorName", "Nobody"),
                ("StartDate", "2025-12-01"),
                ("EndDate", "2026-01-01")));
        var notice = await client.PostAsync(
            $"/BookClubs/{club.Slug}/Notices",
            Form(token, ("NewNotice.Body", "Unauthorized notice.")));

        AssertAccessDenied(create);
        AssertAccessDenied(book);
        AssertAccessDenied(notice);
        await WithServicesAsync(async services =>
        {
            var context = services.GetRequiredService<BlogDbContext>();
            Assert.False(await context.BookClubs.AnyAsync(existing =>
                existing.Name == "Unauthorized Club"));
            Assert.False(await context.ClubBooks.AnyAsync(existing =>
                existing.ClubId == club.Id));
            Assert.False(await context.ClubNotices.AnyAsync(existing =>
                existing.ClubId == club.Id));
        });
    }

    [Fact]
    public async Task AdminCanCreateClubAddBookAndPostNotice()
    {
        var admin = await CreateUserAsync("Club Builder Admin", isAdmin: true);
        using var client = await LoginAsync(admin.Email!);
        var createToken = await GetAntiforgeryTokenAsync(
            client,
            "/BookClubs/Create");
        var slug = $"built-{Guid.NewGuid():N}";

        var create = await client.PostAsync(
            "/BookClubs/Create",
            Form(createToken,
                ("Name", "Built Club"),
                ("Slug", slug),
                ("Description", "A club created in tests.")));

        Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);
        Assert.Equal(
            $"/BookClubs/{slug}",
            LocationPath(create));

        var bookToken = await GetAntiforgeryTokenAsync(
            client,
            $"/BookClubs/{slug}/books/Create");
        using var bookForm = MultipartForm(
            bookToken,
            [
                ("Title", "Test Novel"),
                ("AuthorName", "A. Writer"),
                ("Notes", "First pick."),
                ("StartDate", DateTime.UtcNow.Date.AddDays(-7).ToString("yyyy-MM-dd")),
                ("EndDate", DateTime.UtcNow.Date.ToString("yyyy-MM-dd"))
            ],
            PngBytes,
            "image/png",
            "cover.png");
        var book = await client.PostAsync(
            $"/BookClubs/{slug}/books/Create",
            bookForm);
        var noticeToken = await GetAntiforgeryTokenAsync(
            client,
            $"/BookClubs/{slug}");
        var notice = await client.PostAsync(
            $"/BookClubs/{slug}/Notices",
            Form(noticeToken, ("NewNotice.Body", "Welcome to the club.")));

        Assert.Equal(HttpStatusCode.Redirect, book.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, notice.StatusCode);
        await WithServicesAsync(async services =>
        {
            var context = services.GetRequiredService<BlogDbContext>();
            var club = await context.BookClubs
                .Include(existing => existing.Memberships)
                .SingleAsync(existing => existing.Slug == slug);
            Assert.Equal("Built Club", club.Name);
            Assert.Equal(admin.Id, club.CreatedById);
            Assert.Contains(club.Memberships, membership =>
                membership.UserId == admin.Id);
            var storedBook = await context.ClubBooks.SingleAsync(existing =>
                existing.ClubId == club.Id && existing.Title == "Test Novel");
            Assert.NotNull(storedBook.ImagePath);
            Assert.NotNull(storedBook.ImageObjectKey);
            Assert.True(await context.ClubNotices.AnyAsync(existing =>
                existing.ClubId == club.Id &&
                existing.Body == "Welcome to the club."));
        });
    }

    [Fact]
    public async Task AdminCanEditAndDeleteNotice()
    {
        var admin = await CreateUserAsync("Notice Editor Admin", isAdmin: true);
        var club = await CreateClubAsync(admin, "edit-delete-notice");
        using var client = await LoginAsync(admin.Email!);

        var createToken = await GetAntiforgeryTokenAsync(
            client,
            $"/BookClubs/{club.Slug}");
        var create = await client.PostAsync(
            $"/BookClubs/{club.Slug}/Notices",
            Form(createToken, ("NewNotice.Body", "Original notice.")));
        Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);

        ClubNotice notice = null!;
        await WithServicesAsync(async services =>
        {
            notice = await services
                .GetRequiredService<BlogDbContext>()
                .ClubNotices
                .SingleAsync(existing =>
                    existing.ClubId == club.Id &&
                    existing.Body == "Original notice.");
        });

        var editToken = await GetAntiforgeryTokenAsync(
            client,
            $"/BookClubs/{club.Slug}");
        var edit = await client.PostAsync(
            $"/BookClubs/{club.Slug}/Notices/{notice.Id}/Edit",
            Form(editToken, ("Body", "Updated notice.")));
        Assert.Equal(HttpStatusCode.Redirect, edit.StatusCode);

        await WithServicesAsync(async services =>
        {
            var stored = await services
                .GetRequiredService<BlogDbContext>()
                .ClubNotices
                .SingleAsync(existing => existing.Id == notice.Id);
            Assert.Equal("Updated notice.", stored.Body);
        });

        var deleteToken = await GetAntiforgeryTokenAsync(
            client,
            $"/BookClubs/{club.Slug}");
        var delete = await client.PostAsync(
            $"/BookClubs/{club.Slug}/Notices/{notice.Id}/Delete",
            Form(deleteToken));
        Assert.Equal(HttpStatusCode.Redirect, delete.StatusCode);

        await WithServicesAsync(async services =>
        {
            Assert.False(await services
                .GetRequiredService<BlogDbContext>()
                .ClubNotices
                .AnyAsync(existing => existing.Id == notice.Id));
        });
    }

    [Fact]
    public async Task JoinThenLeaveUpdatesMembership()
    {
        var admin = await CreateUserAsync("Club Join Admin", isAdmin: true);
        var user = await CreateUserAsync("Club Joiner");
        var club = await CreateClubAsync(admin, "join-leave");
        using var client = await LoginAsync(user.Email!);
        var token = await GetAntiforgeryTokenAsync(
            client,
            $"/BookClubs/{club.Slug}");

        var join = await client.PostAsync(
            $"/BookClubs/{club.Slug}/Join",
            Form(token));
        Assert.Equal(HttpStatusCode.Redirect, join.StatusCode);
        await AssertMembershipAsync(club.Id, user.Id, expected: true);

        var leaveToken = await GetAntiforgeryTokenAsync(
            client,
            $"/BookClubs/{club.Slug}");
        var leave = await client.PostAsync(
            $"/BookClubs/{club.Slug}/Leave",
            Form(leaveToken));
        Assert.Equal(HttpStatusCode.Redirect, leave.StatusCode);
        await AssertMembershipAsync(club.Id, user.Id, expected: false);
    }

    [Fact]
    public async Task AdminCanPostDiscussionWithoutJoiningAgain()
    {
        var admin = await CreateUserAsync("Club Poster Admin", isAdmin: true);
        var other = await CreateUserAsync("Club Other Admin", isAdmin: true);
        var club = await CreateClubAsync(other, "admin-can-talk");
        using var client = await LoginAsync(admin.Email!);
        var token = await GetAntiforgeryTokenAsync(
            client,
            $"/BookClubs/{club.Slug}");

        var response = await client.PostAsync(
            $"/BookClubs/{club.Slug}/Discussions",
            Form(token, ("NewDiscussion.Body", "Admin notice-board chat.")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await WithServicesAsync(async services =>
        {
            Assert.True(await services
                .GetRequiredService<BlogDbContext>()
                .DiscussionPosts
                .AnyAsync(post =>
                    post.ClubId == club.Id &&
                    post.AuthorId == admin.Id &&
                    post.Body == "Admin notice-board chat."));
        });
    }

    [Fact]
    public async Task AdminCanEditAndDeleteClubAndBook()
    {
        var admin = await CreateUserAsync("Club Editor Admin", isAdmin: true);
        var club = await CreateClubAsync(admin, "edit-delete-club");
        var book = await CreateBookAsync(club, "Editable Book", daysFromToday: 0);
        using var client = await LoginAsync(admin.Email!);
        var editToken = await GetAntiforgeryTokenAsync(
            client,
            $"/BookClubs/{club.Slug}/books/{book.Id}/Edit");

        var editBook = await client.PostAsync(
            $"/BookClubs/{club.Slug}/books/{book.Id}/Edit",
            Form(editToken,
                ("Title", "Renamed Book"),
                ("AuthorName", "New Author"),
                ("StartDate", DateTime.UtcNow.Date.AddDays(-7).ToString("yyyy-MM-dd")),
                ("EndDate", DateTime.UtcNow.Date.ToString("yyyy-MM-dd"))));
        Assert.Equal(HttpStatusCode.Redirect, editBook.StatusCode);

        var deleteBookToken = await GetAntiforgeryTokenAsync(
            client,
            $"/BookClubs/{club.Slug}/books/{book.Id}/Delete");
        var deleteBook = await client.PostAsync(
            $"/BookClubs/{club.Slug}/books/{book.Id}/Delete",
            Form(deleteBookToken));
        Assert.Equal(HttpStatusCode.Redirect, deleteBook.StatusCode);

        var deleteClubToken = await GetAntiforgeryTokenAsync(
            client,
            $"/BookClubs/{club.Slug}/Delete");
        var deleteClub = await client.PostAsync(
            $"/BookClubs/{club.Slug}/Delete",
            Form(deleteClubToken));
        Assert.Equal(HttpStatusCode.Redirect, deleteClub.StatusCode);

        await WithServicesAsync(async services =>
        {
            var context = services.GetRequiredService<BlogDbContext>();
            Assert.False(await context.ClubBooks.AnyAsync(existing =>
                existing.Id == book.Id));
            Assert.False(await context.BookClubs.AnyAsync(existing =>
                existing.Id == club.Id));
        });
    }

    [Fact]
    public async Task AuthorCanEditAndDeleteOwnDiscussion()
    {
        var admin = await CreateUserAsync("Club Disc Admin", isAdmin: true);
        var member = await CreateUserAsync("Club Disc Member");
        var club = await CreateClubAsync(admin, "edit-own-discussion");
        await AddMembershipAsync(club, member);
        var post = await CreateDiscussionAsync(club, member, "Original message.");
        using var client = await LoginAsync(member.Email!);
        var editToken = await GetAntiforgeryTokenAsync(
            client,
            $"/BookClubs/{club.Slug}/discussions/{post.Id}/Edit");

        var edit = await client.PostAsync(
            $"/BookClubs/{club.Slug}/discussions/{post.Id}/Edit",
            Form(editToken, ("Body", "Updated message.")));
        Assert.Equal(HttpStatusCode.Redirect, edit.StatusCode);

        var deleteToken = await GetAntiforgeryTokenAsync(
            client,
            $"/BookClubs/{club.Slug}/discussions/{post.Id}/Delete");
        var delete = await client.PostAsync(
            $"/BookClubs/{club.Slug}/discussions/{post.Id}/Delete",
            Form(deleteToken));
        Assert.Equal(HttpStatusCode.Redirect, delete.StatusCode);

        await WithServicesAsync(async services =>
        {
            var context = services.GetRequiredService<BlogDbContext>();
            Assert.False(await context.DiscussionPosts.AnyAsync(existing =>
                existing.Id == post.Id));
        });
    }

    [Fact]
    public async Task OtherMemberCannotEditOrDeleteDiscussion()
    {
        var admin = await CreateUserAsync("Club Guard Admin", isAdmin: true);
        var author = await CreateUserAsync("Club Author Member");
        var other = await CreateUserAsync("Club Other Member");
        var club = await CreateClubAsync(admin, "protect-discussion");
        await AddMembershipAsync(club, author);
        await AddMembershipAsync(club, other);
        var post = await CreateDiscussionAsync(club, author, "Leave this alone.");
        using var client = await LoginAsync(other.Email!);
        var token = await GetAntiforgeryTokenAsync(
            client,
            $"/BookClubs/{club.Slug}");

        var edit = await client.PostAsync(
            $"/BookClubs/{club.Slug}/discussions/{post.Id}/Edit",
            Form(token, ("Body", "Stolen edit.")));
        var delete = await client.PostAsync(
            $"/BookClubs/{club.Slug}/discussions/{post.Id}/Delete",
            Form(token));

        AssertAccessDenied(edit);
        AssertAccessDenied(delete);
        await WithServicesAsync(async services =>
        {
            var stored = await services
                .GetRequiredService<BlogDbContext>()
                .DiscussionPosts
                .SingleAsync(existing => existing.Id == post.Id);
            Assert.Equal("Leave this alone.", stored.Body);
        });
    }

    [Fact]
    public async Task AdminCanPinDiscussionAndSortPosts()
    {
        var admin = await CreateUserAsync("Discussion Sort Admin", isAdmin: true);
        var club = await CreateClubAsync(admin, "sort-and-pin");
        var oldest = await CreateDiscussionAsync(club, admin, "Oldest post.");
        var pinned = await CreateDiscussionAsync(club, admin, "Pinned post.");
        var newest = await CreateDiscussionAsync(club, admin, "Newest post.");
        await WithServicesAsync(async services =>
        {
            var context = services.GetRequiredService<BlogDbContext>();
            var posts = await context.DiscussionPosts
                .Where(post => post.ClubId == club.Id)
                .ToListAsync();
            posts.Single(post => post.Id == oldest.Id).CreatedAt =
                DateTime.UtcNow.AddHours(-3);
            posts.Single(post => post.Id == pinned.Id).CreatedAt =
                DateTime.UtcNow.AddHours(-2);
            posts.Single(post => post.Id == newest.Id).CreatedAt =
                DateTime.UtcNow.AddHours(-1);
            await context.SaveChangesAsync();
        });

        using var client = await LoginAsync(admin.Email!);
        var token = await GetAntiforgeryTokenAsync(
            client,
            $"/BookClubs/{club.Slug}");
        var pin = await client.PostAsync(
            $"/BookClubs/{club.Slug}/discussions/{pinned.Id}/Pin?sort=newest",
            Form(token));
        Assert.Equal(HttpStatusCode.Redirect, pin.StatusCode);

        var newestPage = await client.GetStringAsync(
            $"/BookClubs/{club.Slug}?sort=newest");
        Assert.Contains("Pinned post.", newestPage);
        Assert.Contains("Newest post.", newestPage);
        Assert.Contains("Oldest post.", newestPage);
        Assert.True(
            newestPage.IndexOf("Pinned post.", StringComparison.Ordinal) <
            newestPage.IndexOf("Newest post.", StringComparison.Ordinal));
        Assert.True(
            newestPage.IndexOf("Newest post.", StringComparison.Ordinal) <
            newestPage.IndexOf("Oldest post.", StringComparison.Ordinal));

        var oldestPage = await client.GetStringAsync(
            $"/BookClubs/{club.Slug}?sort=oldest");
        Assert.True(
            oldestPage.IndexOf("Pinned post.", StringComparison.Ordinal) <
            oldestPage.IndexOf("Oldest post.", StringComparison.Ordinal));
        Assert.True(
            oldestPage.IndexOf("Oldest post.", StringComparison.Ordinal) <
            oldestPage.IndexOf("Newest post.", StringComparison.Ordinal));

        await WithServicesAsync(async services =>
        {
            Assert.True((await services
                .GetRequiredService<BlogDbContext>()
                .DiscussionPosts
                .SingleAsync(post => post.Id == pinned.Id)).IsPinned);
        });
    }

    [Fact]
    public async Task MemberCanCreateAndVoteInClubAndBookPolls()
    {
        var admin = await CreateUserAsync("Poll Admin", isAdmin: true);
        var member = await CreateUserAsync("Poll Member");
        var club = await CreateClubAsync(admin, "discussion-polls");
        var book = await CreateBookAsync(club, "Poll Book", daysFromToday: 1);
        await AddMembershipAsync(club, member);

        using var memberClient = await LoginAsync(member.Email!);
        var clubToken = await GetAntiforgeryTokenAsync(
            memberClient,
            $"/BookClubs/{club.Slug}");
        var createClubPoll = await memberClient.PostAsync(
            $"/BookClubs/{club.Slug}/polls",
            Form(
                clubToken,
                ("Title", "Which chapter next?"),
                ("Options[0]", "Chapter one"),
                ("Options[1]", "Chapter two")));
        Assert.Equal(HttpStatusCode.Redirect, createClubPoll.StatusCode);

        var bookToken = await GetAntiforgeryTokenAsync(
            memberClient,
            $"/BookClubs/{club.Slug}/books/{book.Id}");
        var createBookPoll = await memberClient.PostAsync(
            $"/BookClubs/{club.Slug}/polls?bookId={book.Id}&threadId=0",
            Form(
                bookToken,
                ("Title", "Favorite character?"),
                ("Options[0]", "Ada"),
                ("Options[1]", "Bert")));
        Assert.Equal(HttpStatusCode.Redirect, createBookPoll.StatusCode);

        int clubPollId = 0;
        int clubPollPostId = 0;
        int chapterTwoId = 0;
        await WithServicesAsync(async services =>
        {
            var context = services.GetRequiredService<BlogDbContext>();
            var polls = await context.DiscussionPolls
                .Include(poll => poll.DiscussionPost)
                .Include(poll => poll.Options)
                .ToListAsync();
            var clubPoll = polls.Single(poll =>
                poll.DiscussionPost.Body == "Which chapter next?");
            var bookPoll = polls.Single(poll =>
                poll.DiscussionPost.Body == "Favorite character?");
            Assert.Null(clubPoll.DiscussionPost.ClubBookId);
            Assert.Equal(book.Id, bookPoll.DiscussionPost.ClubBookId);
            clubPollId = clubPoll.Id;
            clubPollPostId = clubPoll.DiscussionPostId;
            chapterTwoId = clubPoll.Options.Single(option =>
                option.Text == "Chapter two").Id;
        });

        var voteToken = await GetAntiforgeryTokenAsync(
            memberClient,
            $"/BookClubs/{club.Slug}");
        var vote = await memberClient.PostAsync(
            $"/BookClubs/{club.Slug}/polls/{clubPollId}/Vote",
            Form(voteToken, ("optionId", chapterTwoId.ToString())));
        Assert.Equal(HttpStatusCode.Redirect, vote.StatusCode);

        var clubPage = await memberClient.GetStringAsync(
            $"/BookClubs/{club.Slug}");
        Assert.Contains("Which chapter next?", clubPage);
        Assert.Contains("Chapter two", clubPage);
        Assert.Contains(member.DisplayName, clubPage);

        var bookPage = await memberClient.GetStringAsync(
            $"/BookClubs/{club.Slug}/books/{book.Id}");
        Assert.Contains("Favorite character?", bookPage);
        Assert.Contains("Ada", bookPage);

        using var adminClient = await LoginAsync(admin.Email!);
        var pinToken = await GetAntiforgeryTokenAsync(
            adminClient,
            $"/BookClubs/{club.Slug}");
        var pin = await adminClient.PostAsync(
            $"/BookClubs/{club.Slug}/discussions/{clubPollPostId}/Pin",
            Form(pinToken));
        Assert.Equal(HttpStatusCode.Redirect, pin.StatusCode);

        await WithServicesAsync(async services =>
        {
            var context = services.GetRequiredService<BlogDbContext>();
            Assert.True((await context.DiscussionPosts.SingleAsync(post =>
                post.Id == clubPollPostId)).IsPinned);
            var storedVote = await context.DiscussionPollVotes
                .SingleAsync(existing =>
                    existing.PollId == clubPollId &&
                    existing.UserId == member.Id);
            Assert.Equal(chapterTwoId, storedVote.OptionId);
        });
    }

    [Fact]
    public async Task MemberCanReplyButNotToAReply()
    {
        var admin = await CreateUserAsync("Club Reply Admin", isAdmin: true);
        var member = await CreateUserAsync("Club Reply Member");
        var club = await CreateClubAsync(admin, "reply-thread");
        await AddMembershipAsync(club, member);
        var parent = await CreateDiscussionAsync(club, admin, "Top-level post.");
        using var client = await LoginAsync(member.Email!);
        var token = await GetAntiforgeryTokenAsync(
            client,
            $"/BookClubs/{club.Slug}");

        var reply = await client.PostAsync(
            $"/BookClubs/{club.Slug}/discussions/{parent.Id}/Reply",
            Form(token, ("Body", "A first-level reply.")));
        Assert.Equal(HttpStatusCode.Redirect, reply.StatusCode);

        DiscussionPost? child = null;
        await WithServicesAsync(async services =>
        {
            child = await services
                .GetRequiredService<BlogDbContext>()
                .DiscussionPosts
                .SingleAsync(post =>
                    post.ParentId == parent.Id &&
                    post.Body == "A first-level reply.");
        });
        Assert.NotNull(child);

        var nested = await client.PostAsync(
            $"/BookClubs/{club.Slug}/discussions/{child.Id}/Reply",
            Form(token, ("Body", "A nested reply.")));
        Assert.Equal(HttpStatusCode.BadRequest, nested.StatusCode);
        await WithServicesAsync(async services =>
        {
            Assert.False(await services
                .GetRequiredService<BlogDbContext>()
                .DiscussionPosts
                .AnyAsync(post => post.ParentId == child.Id));
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
                var roleManager =
                    services.GetRequiredService<RoleManager<IdentityRole>>();
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
                Description = "Integration test club.",
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

    private async Task<ClubBook> CreateBookAsync(
        BookClub club,
        string title,
        int daysFromToday)
    {
        ClubBook? created = null;
        await WithServicesAsync(async services =>
        {
            var context = services.GetRequiredService<BlogDbContext>();
            created = new ClubBook
            {
                ClubId = club.Id,
                Title = title,
                AuthorName = "Test Author",
                ImagePath =
                    $"https://images.example.test/{Guid.NewGuid():N}.jpg",
                StartDate = DateTime.SpecifyKind(
                    DateTime.UtcNow.Date.AddDays(daysFromToday - 7),
                    DateTimeKind.Utc),
                EndDate = DateTime.SpecifyKind(
                    DateTime.UtcNow.Date.AddDays(daysFromToday),
                    DateTimeKind.Utc),
                CreatedAt = DateTime.UtcNow
            };
            context.ClubBooks.Add(created);
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
        string body,
        int? parentId = null,
        int? clubBookId = null)
    {
        DiscussionPost? created = null;
        await WithServicesAsync(async services =>
        {
            var context = services.GetRequiredService<BlogDbContext>();
            created = new DiscussionPost
            {
                ClubId = club.Id,
                ClubBookId = clubBookId,
                ParentId = parentId,
                AuthorId = author.Id,
                Body = body,
                CreatedAt = DateTime.UtcNow
            };
            context.DiscussionPosts.Add(created);
            await context.SaveChangesAsync();
        });
        return created!;
    }

    private async Task AssertMembershipAsync(
        int clubId,
        string userId,
        bool expected)
    {
        await WithServicesAsync(async services =>
        {
            var exists = await services
                .GetRequiredService<BlogDbContext>()
                .BookClubMemberships
                .AnyAsync(membership =>
                    membership.ClubId == clubId &&
                    membership.UserId == userId);
            Assert.Equal(expected, exists);
        });
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

    private static MultipartFormDataContent MultipartForm(
        string token,
        IEnumerable<(string Key, string Value)> fields,
        byte[] fileContent,
        string contentType,
        string fileName)
    {
        var form = new MultipartFormDataContent();
        foreach (var (key, value) in fields.Append(
                     ("__RequestVerificationToken", token)))
        {
            form.Add(new StringContent(value), key);
        }

        var file = new ByteArrayContent(fileContent);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "CoverImage", fileName);
        return form;
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

    private static string LocationPath(HttpResponseMessage response)
    {
        var location = response.Headers.Location;
        Assert.NotNull(location);
        return location.IsAbsoluteUri
            ? location.AbsolutePath
            : location.OriginalString.Split('?')[0];
    }

    private static string UniqueEmail(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}@example.test";
}
