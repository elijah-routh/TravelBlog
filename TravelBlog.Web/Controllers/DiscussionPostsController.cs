using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelBlog.Web.Authorization;
using TravelBlog.Web.Data;
using TravelBlog.Web.Models;

namespace TravelBlog.Web.Controllers;

[Authorize]
[Route("BookClubs/{slug}/discussions")]
public class DiscussionPostsController : Controller
{
    private readonly BlogDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public DiscussionPostsController(
        BlogDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet("{id:int}/Edit")]
    public async Task<IActionResult> Edit(string slug, int id)
    {
        var post = await FindPostAsync(slug, id);
        if (post is null)
        {
            return NotFound();
        }

        if (!CanManage(post))
        {
            return Forbid();
        }

        return View(new EditDiscussionPostViewModel
        {
            Id = post.Id,
            ClubSlug = slug,
            ClubBookId = post.ClubBookId,
            Body = post.Body
        });
    }

    [HttpPost("{id:int}/Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        string slug,
        int id,
        EditDiscussionPostViewModel model)
    {
        var post = await FindPostAsync(slug, id, asNoTracking: false);
        if (post is null)
        {
            return NotFound();
        }

        if (!CanManage(post))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            model.Id = id;
            model.ClubSlug = slug;
            model.ClubBookId = post.ClubBookId;
            return View(model);
        }

        post.Body = model.Body.Trim();
        post.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["StatusMessage"] = "Message updated.";
        return RedirectToThread(post, slug);
    }

    [HttpGet("{id:int}/Delete")]
    public async Task<IActionResult> Delete(string slug, int id)
    {
        var post = await FindPostAsync(slug, id);
        if (post is null)
        {
            return NotFound();
        }

        if (!CanManage(post))
        {
            return Forbid();
        }

        ViewData["ClubSlug"] = slug;
        ViewData["ClubBookId"] = post.ClubBookId;
        return View(post);
    }

    [HttpPost("{id:int}/Delete")]
    [ValidateAntiForgeryToken]
    [ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(string slug, int id)
    {
        var post = await FindPostAsync(slug, id, asNoTracking: false);
        if (post is null)
        {
            return NotFound();
        }

        if (!CanManage(post))
        {
            return Forbid();
        }

        var bookId = post.ClubBookId;
        var threadId = post.BookDiscussionThreadId;
        _context.DiscussionPosts.Remove(post);
        await _context.SaveChangesAsync();

        TempData["StatusMessage"] = "Message deleted.";
        return bookId is int storedBookId
            ? RedirectToAction(
                "Details",
                "ClubBooks",
                new
                {
                    clubSlug = slug,
                    bookId = storedBookId,
                    thread = threadId ?? 0
                })
            : RedirectToAction("Details", "BookClubs", new { slug });
    }

    [HttpPost("{id:int}/Reply")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(
        string slug,
        int id,
        string? sort,
        AddDiscussionPostViewModel model)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var parent = await FindPostAsync(slug, id);
        if (parent is null)
        {
            return NotFound();
        }

        if (parent.ParentId is not null)
        {
            return BadRequest();
        }

        if (!await CanPostAsync(parent.ClubId, userId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "A reply message is required.";
            return RedirectToThread(parent, slug, sort);
        }

        _context.DiscussionPosts.Add(new DiscussionPost
        {
            ClubId = parent.ClubId,
            ClubBookId = parent.ClubBookId,
            BookDiscussionThreadId = parent.BookDiscussionThreadId,
            ParentId = parent.Id,
            Body = model.Body.Trim(),
            AuthorId = userId,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        TempData["StatusMessage"] = "Reply posted.";
        return RedirectToThread(parent, slug, sort);
    }

    [HttpPost("{id:int}/Pin")]
    [Authorize(Roles = RoleNames.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePin(
        string slug,
        int id,
        string? sort)
    {
        var post = await FindPostAsync(slug, id, asNoTracking: false);
        if (post is null)
        {
            return NotFound();
        }

        if (post.ParentId is not null)
        {
            return BadRequest();
        }

        post.IsPinned = !post.IsPinned;
        await _context.SaveChangesAsync();

        TempData["StatusMessage"] = post.IsPinned
            ? "Discussion post pinned."
            : "Discussion post unpinned.";
        return RedirectToThread(post, slug, sort);
    }

    private async Task<DiscussionPost?> FindPostAsync(
        string slug,
        int id,
        bool asNoTracking = true)
    {
        var query = asNoTracking
            ? _context.DiscussionPosts.AsNoTracking()
            : _context.DiscussionPosts.AsQueryable();

        return await query
            .Include(post => post.Club)
            .Include(post => post.Author)
            .FirstOrDefaultAsync(post =>
                post.Id == id &&
                post.Club.Slug == slug);
    }

    private bool CanManage(DiscussionPost post)
    {
        var userId = _userManager.GetUserId(User);
        return OwnerAccess.IsAdminOrOwner(
            User.IsInRole(RoleNames.Admin),
            userId,
            post.AuthorId);
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

    private IActionResult RedirectToThread(
        DiscussionPost post,
        string slug,
        string? sort = null) =>
        post.ClubBookId is int bookId
            ? RedirectToAction(
                "Details",
                "ClubBooks",
                new
                {
                    clubSlug = slug,
                    bookId,
                    thread = post.BookDiscussionThreadId ?? 0,
                    sort = string.IsNullOrWhiteSpace(sort)
                        ? null
                        : DiscussionSortOrder.Normalize(sort)
                })
            : RedirectToAction(
                "Details",
                "BookClubs",
                new
                {
                    slug,
                    sort = string.IsNullOrWhiteSpace(sort)
                        ? null
                        : DiscussionSortOrder.Normalize(sort)
                });
}
