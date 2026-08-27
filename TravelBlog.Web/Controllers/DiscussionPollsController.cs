using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelBlog.Web.Authorization;
using TravelBlog.Web.Data;
using TravelBlog.Web.Models;

namespace TravelBlog.Web.Controllers;

[Authorize]
[Route("BookClubs/{slug}/polls")]
public class DiscussionPollsController : Controller
{
    private readonly BlogDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public DiscussionPollsController(
        BlogDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        string slug,
        int? bookId,
        int? threadId,
        string? sort,
        CreateDiscussionPollViewModel model)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var club = await _context.BookClubs
            .AsNoTracking()
            .FirstOrDefaultAsync(existing => existing.Slug == slug);
        if (club is null)
        {
            return NotFound();
        }

        ClubBook? book = null;
        if (bookId is int storedBookId)
        {
            book = await _context.ClubBooks
                .AsNoTracking()
                .FirstOrDefaultAsync(existing =>
                    existing.Id == storedBookId &&
                    existing.ClubId == club.Id);
            if (book is null)
            {
                return NotFound();
            }
        }

        BookDiscussionThread? discussionThread = null;
        if (threadId is > 0)
        {
            if (book is null)
            {
                return BadRequest();
            }

            discussionThread = await _context.BookDiscussionThreads
                .AsNoTracking()
                .FirstOrDefaultAsync(existing =>
                    existing.Id == threadId &&
                    existing.ClubBookId == book.Id);
            if (discussionThread is null)
            {
                return NotFound();
            }
        }

        if (!await CanPostAsync(club.Id, userId))
        {
            return Forbid();
        }

        var options = model.Options
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Select(option => option.Trim())
            .ToList();
        if (options.Any(option => option.Length > 200))
        {
            ModelState.AddModelError(
                nameof(model.Options),
                "Poll options cannot exceed 200 characters.");
        }

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = ModelState.Values
                .SelectMany(value => value.Errors)
                .Select(error => error.ErrorMessage)
                .FirstOrDefault()
                ?? "Enter a valid poll.";
            return RedirectToDiscussion(
                slug,
                book?.Id,
                discussionThread?.Id ?? 0,
                sort);
        }

        var post = new DiscussionPost
        {
            ClubId = club.Id,
            ClubBookId = book?.Id,
            BookDiscussionThreadId = discussionThread?.Id,
            Body = model.Title.Trim(),
            AuthorId = userId,
            CreatedAt = DateTime.UtcNow,
            Poll = new DiscussionPoll
            {
                Options = options
                    .Select((option, index) => new DiscussionPollOption
                    {
                        Text = option,
                        SortOrder = index
                    })
                    .ToList()
            }
        };
        _context.DiscussionPosts.Add(post);
        await _context.SaveChangesAsync();

        TempData["StatusMessage"] = "Poll created.";
        return RedirectToDiscussion(
            slug,
            book?.Id,
            discussionThread?.Id ?? 0,
            sort);
    }

    [HttpPost("{pollId:int}/Vote")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Vote(
        string slug,
        int pollId,
        int optionId,
        string? sort)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var poll = await _context.DiscussionPolls
            .Include(existing => existing.DiscussionPost)
                .ThenInclude(post => post.Club)
            .Include(existing => existing.Options)
            .FirstOrDefaultAsync(existing =>
                existing.Id == pollId &&
                existing.DiscussionPost.Club.Slug == slug);
        if (poll is null)
        {
            return NotFound();
        }

        if (!await CanPostAsync(poll.DiscussionPost.ClubId, userId))
        {
            return Forbid();
        }

        if (!poll.Options.Any(option => option.Id == optionId))
        {
            return BadRequest();
        }

        var vote = await _context.DiscussionPollVotes
            .FirstOrDefaultAsync(existing =>
                existing.PollId == poll.Id &&
                existing.UserId == userId);
        if (vote is null)
        {
            _context.DiscussionPollVotes.Add(new DiscussionPollVote
            {
                PollId = poll.Id,
                OptionId = optionId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            vote.OptionId = optionId;
            vote.CreatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        TempData["StatusMessage"] = "Vote recorded.";
        return RedirectToDiscussion(
            slug,
            poll.DiscussionPost.ClubBookId,
            poll.DiscussionPost.BookDiscussionThreadId ?? 0,
            sort);
    }

    private async Task<bool> CanPostAsync(int clubId, string userId)
    {
        if (User.IsInRole(RoleNames.Admin))
        {
            return true;
        }

        return await _context.BookClubMemberships.AnyAsync(membership =>
            membership.ClubId == clubId && membership.UserId == userId);
    }

    private IActionResult RedirectToDiscussion(
        string slug,
        int? bookId,
        int threadId,
        string? sort) =>
        bookId is int storedBookId
            ? RedirectToAction(
                "Details",
                "ClubBooks",
                new
                {
                    clubSlug = slug,
                    bookId = storedBookId,
                    thread = threadId,
                    sort = DiscussionSortOrder.Normalize(sort)
                })
            : RedirectToAction(
                "Details",
                "BookClubs",
                new
                {
                    slug,
                    sort = DiscussionSortOrder.Normalize(sort)
                });
}
