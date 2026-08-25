using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelBlog.Web.Authorization;
using TravelBlog.Web.Data;
using TravelBlog.Web.Models;
using TravelBlog.Web.Services;

namespace TravelBlog.Web.Controllers;

[Route("BookClubs/{clubSlug}/books")]
public class ClubBooksController : Controller
{
    private readonly BlogDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IImageStorage _imageStorage;
    private readonly ILogger<ClubBooksController> _logger;

    public ClubBooksController(
        BlogDbContext context,
        UserManager<ApplicationUser> userManager,
        IImageStorage imageStorage,
        ILogger<ClubBooksController> logger)
    {
        _context = context;
        _userManager = userManager;
        _imageStorage = imageStorage;
        _logger = logger;
    }

    [HttpGet("Create")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Create(string clubSlug)
    {
        var club = await FindClubAsync(clubSlug);
        if (club is null)
        {
            return NotFound();
        }

        ViewData["ClubName"] = club.Name;
        ViewData["ClubSlug"] = club.Slug;
        return View(new AddClubBookViewModel());
    }

    [HttpPost("Create")]
    [Authorize(Roles = RoleNames.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        string clubSlug,
        AddClubBookViewModel model)
    {
        var club = await FindClubAsync(clubSlug);
        if (club is null)
        {
            return NotFound();
        }

        ViewData["ClubName"] = club.Name;
        ViewData["ClubSlug"] = club.Slug;

        if (model.CoverImage is null)
        {
            ModelState.AddModelError(
                nameof(model.CoverImage),
                "A book cover is required.");
        }

        var imageValidation = await ValidateCoverAsync(
            model.CoverImage,
            CancellationToken.None);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        StoredImage? uploadedImage = null;
        if (model.CoverImage is not null)
        {
            uploadedImage = await UploadCoverAsync(
                model.CoverImage,
                imageValidation!,
                CancellationToken.None);
        }

        var book = new ClubBook
        {
            ClubId = club.Id,
            Title = model.Title.Trim(),
            AuthorName = model.AuthorName.Trim(),
            Notes = string.IsNullOrWhiteSpace(model.Notes)
                ? null
                : model.Notes.Trim(),
            ImagePath = uploadedImage?.PublicUrl,
            ImageObjectKey = uploadedImage?.ObjectKey,
            ReadingDate = DateTime.SpecifyKind(
                model.ReadingDate.Date,
                DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow
        };

        _context.ClubBooks.Add(book);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch
        {
            if (uploadedImage is not null)
            {
                await DeleteCoverBestEffortAsync(uploadedImage.ObjectKey);
            }

            throw;
        }

        TempData["StatusMessage"] = $"{book.Title} was added to the timeline.";
        return RedirectToAction(
            "Details",
            "BookClubs",
            new { slug = club.Slug });
    }

    [HttpGet("{bookId:int}/Edit")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Edit(string clubSlug, int bookId)
    {
        var book = await FindBookAsync(clubSlug, bookId);
        if (book is null)
        {
            return NotFound();
        }

        ViewData["ClubName"] = book.Club.Name;
        ViewData["ClubSlug"] = book.Club.Slug;
        return View(new AddClubBookViewModel
        {
            Title = book.Title,
            AuthorName = book.AuthorName,
            Notes = book.Notes,
            CurrentImagePath = book.ImagePath,
            ReadingDate = book.ReadingDate
        });
    }

    [HttpPost("{bookId:int}/Edit")]
    [Authorize(Roles = RoleNames.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        string clubSlug,
        int bookId,
        AddClubBookViewModel model)
    {
        var book = await FindBookAsync(clubSlug, bookId, asNoTracking: false);
        if (book is null)
        {
            return NotFound();
        }

        ViewData["ClubName"] = book.Club.Name;
        ViewData["ClubSlug"] = book.Club.Slug;
        model.CurrentImagePath = book.ImagePath;

        if (string.IsNullOrWhiteSpace(book.ImagePath) &&
            model.CoverImage is null)
        {
            ModelState.AddModelError(
                nameof(model.CoverImage),
                "A book cover is required.");
        }

        var imageValidation = await ValidateCoverAsync(
            model.CoverImage,
            CancellationToken.None);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        StoredImage? uploadedImage = null;
        if (model.CoverImage is not null)
        {
            uploadedImage = await UploadCoverAsync(
                model.CoverImage,
                imageValidation!,
                CancellationToken.None);
        }

        var previousObjectKey = book.ImageObjectKey;
        book.Title = model.Title.Trim();
        book.AuthorName = model.AuthorName.Trim();
        book.Notes = string.IsNullOrWhiteSpace(model.Notes)
            ? null
            : model.Notes.Trim();
        if (uploadedImage is not null)
        {
            book.ImagePath = uploadedImage.PublicUrl;
            book.ImageObjectKey = uploadedImage.ObjectKey;
        }
        book.ReadingDate = DateTime.SpecifyKind(
            model.ReadingDate.Date,
            DateTimeKind.Utc);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch
        {
            if (uploadedImage is not null)
            {
                await DeleteCoverBestEffortAsync(uploadedImage.ObjectKey);
            }

            throw;
        }

        if (uploadedImage is not null &&
            !string.IsNullOrWhiteSpace(previousObjectKey))
        {
            await DeleteCoverBestEffortAsync(previousObjectKey);
        }

        TempData["StatusMessage"] = $"{book.Title} was updated.";
        return RedirectToAction(nameof(Details), new { clubSlug, bookId });
    }

    [HttpGet("{bookId:int}/Delete")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Delete(string clubSlug, int bookId)
    {
        var book = await FindBookAsync(clubSlug, bookId);
        if (book is null)
        {
            return NotFound();
        }

        return View(new ClubBookDeleteViewModel
        {
            Id = book.Id,
            ClubName = book.Club.Name,
            ClubSlug = book.Club.Slug,
            Title = book.Title,
            AuthorName = book.AuthorName
        });
    }

    [HttpPost("{bookId:int}/Delete")]
    [Authorize(Roles = RoleNames.Admin)]
    [ValidateAntiForgeryToken]
    [ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(string clubSlug, int bookId)
    {
        var book = await FindBookAsync(clubSlug, bookId, asNoTracking: false);
        if (book is null)
        {
            return NotFound();
        }

        var title = book.Title;
        var slug = book.Club.Slug;
        var objectKey = book.ImageObjectKey;
        _context.ClubBooks.Remove(book);
        await _context.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(objectKey))
        {
            await DeleteCoverBestEffortAsync(objectKey);
        }

        TempData["StatusMessage"] = $"{title} was removed from the timeline.";
        return RedirectToAction("Details", "BookClubs", new { slug });
    }

    [HttpGet("{bookId:int}")]
    public async Task<IActionResult> Details(
        string clubSlug,
        int bookId,
        int? thread = null)
    {
        var viewModel = await BuildDetailsViewModelAsync(
            clubSlug,
            bookId,
            thread);
        return viewModel is null ? NotFound() : View(viewModel);
    }

    [HttpPost("{bookId:int}/Threads")]
    [Authorize(Roles = RoleNames.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateThread(
        string clubSlug,
        int bookId,
        [Bind(Prefix = "NewThread")]
        CreateBookDiscussionThreadViewModel model)
    {
        var book = await FindBookAsync(clubSlug, bookId);
        if (book is null)
        {
            return NotFound();
        }

        var title = model.Title.Trim();
        if (await _context.BookDiscussionThreads.AnyAsync(thread =>
            thread.ClubBookId == bookId &&
            thread.Title == title))
        {
            ModelState.AddModelError(
                nameof(model.Title),
                "A thread with that title already exists.");
        }

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] =
                ModelState.Values
                    .SelectMany(value => value.Errors)
                    .Select(error => error.ErrorMessage)
                    .FirstOrDefault()
                ?? "Enter a valid thread title.";
            return RedirectToAction(nameof(Details), new { clubSlug, bookId });
        }

        var discussionThread = new BookDiscussionThread
        {
            ClubBookId = bookId,
            Title = title,
            CreatedAt = DateTime.UtcNow
        };
        _context.BookDiscussionThreads.Add(discussionThread);
        await _context.SaveChangesAsync();

        TempData["StatusMessage"] = $"{title} discussion created.";
        return RedirectToAction(
            nameof(Details),
            new { clubSlug, bookId, thread = discussionThread.Id });
    }

    [HttpPost("{bookId:int}/Threads/{threadId:int}/Delete")]
    [Authorize(Roles = RoleNames.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteThread(
        string clubSlug,
        int bookId,
        int threadId)
    {
        var discussionThread = await _context.BookDiscussionThreads
            .Include(thread => thread.ClubBook)
                .ThenInclude(book => book.Club)
            .FirstOrDefaultAsync(thread =>
                thread.Id == threadId &&
                thread.ClubBookId == bookId &&
                thread.ClubBook.Club.Slug == clubSlug);
        if (discussionThread is null)
        {
            return NotFound();
        }

        _context.BookDiscussionThreads.Remove(discussionThread);
        await _context.SaveChangesAsync();

        TempData["StatusMessage"] =
            $"{discussionThread.Title} discussion deleted.";
        return RedirectToAction(nameof(Details), new { clubSlug, bookId });
    }

    [HttpPost("{bookId:int}/Discussions")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddDiscussion(
        string clubSlug,
        int bookId,
        int threadId,
        [Bind(Prefix = "NewDiscussion")] AddDiscussionPostViewModel model)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var book = await _context.ClubBooks
            .Include(existing => existing.Club)
            .FirstOrDefaultAsync(existing =>
                existing.Id == bookId &&
                existing.Club.Slug == clubSlug);
        if (book is null)
        {
            return NotFound();
        }

        if (!await CanPostAsync(book.ClubId, userId))
        {
            return Forbid();
        }

        BookDiscussionThread? discussionThread = null;
        if (threadId > 0)
        {
            discussionThread = await _context.BookDiscussionThreads
                .AsNoTracking()
                .FirstOrDefaultAsync(thread =>
                    thread.Id == threadId &&
                    thread.ClubBookId == book.Id);
            if (discussionThread is null)
            {
                return NotFound();
            }
        }

        if (!ModelState.IsValid)
        {
            var invalidView = await BuildDetailsViewModelAsync(
                clubSlug,
                bookId,
                threadId);
            if (invalidView is null)
            {
                return NotFound();
            }

            invalidView.NewDiscussion = model;
            return View(nameof(Details), invalidView);
        }

        _context.DiscussionPosts.Add(new DiscussionPost
        {
            ClubId = book.ClubId,
            ClubBookId = book.Id,
            BookDiscussionThreadId = discussionThread?.Id,
            Body = model.Body.Trim(),
            AuthorId = userId,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        TempData["StatusMessage"] = "Message posted.";
        return RedirectToAction(
            nameof(Details),
            new { clubSlug, bookId, thread = threadId });
    }

    private async Task<BookClub?> FindClubAsync(string clubSlug) =>
        await _context.BookClubs
            .AsNoTracking()
            .FirstOrDefaultAsync(club => club.Slug == clubSlug);

    private async Task<ClubBook?> FindBookAsync(
        string clubSlug,
        int bookId,
        bool asNoTracking = true)
    {
        var query = asNoTracking
            ? _context.ClubBooks.AsNoTracking()
            : _context.ClubBooks.AsQueryable();

        return await query
            .Include(book => book.Club)
            .FirstOrDefaultAsync(book =>
                book.Id == bookId &&
                book.Club.Slug == clubSlug);
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

    private async Task<ClubBookDetailsViewModel?> BuildDetailsViewModelAsync(
        string clubSlug,
        int bookId,
        int? requestedThreadId = null)
    {
        var book = await _context.ClubBooks
            .AsNoTracking()
            .Include(existing => existing.Club)
                .ThenInclude(club => club.Memberships)
            .Include(existing => existing.DiscussionPosts)
                .ThenInclude(post => post.Author)
            .Include(existing => existing.DiscussionThreads)
            .FirstOrDefaultAsync(existing =>
                existing.Id == bookId &&
                existing.Club.Slug == clubSlug);

        if (book is null)
        {
            return null;
        }

        var clubBooks = await _context.ClubBooks
            .AsNoTracking()
            .Where(existing => existing.ClubId == book.ClubId)
            .ToListAsync();

        var userId = _userManager.GetUserId(User);
        var isAdmin = User.IsInRole(RoleNames.Admin);
        var isMember = !string.IsNullOrWhiteSpace(userId) &&
            (book.Club.Memberships?.Any(membership =>
                membership.UserId == userId) ?? false);
        var now = DateTime.UtcNow;
        var currentBook = ClubBookTimeline.CurrentBook(clubBooks, now);
        var threadItems = new List<BookDiscussionThreadViewModel>
        {
            new()
            {
                Id = 0,
                Title = "General",
                CanDelete = false,
                Posts = DiscussionThreadMapper.Build(
                    book.DiscussionPosts.Where(post =>
                        post.BookDiscussionThreadId is null),
                    book.Club.Slug,
                    userId,
                    isAdmin,
                    isAdmin || isMember)
            }
        };
        threadItems.AddRange(book.DiscussionThreads
            .OrderBy(thread => thread.CreatedAt)
            .ThenBy(thread => thread.Title)
            .Select(thread => new BookDiscussionThreadViewModel
            {
                Id = thread.Id,
                Title = thread.Title,
                CanDelete = isAdmin,
                Posts = DiscussionThreadMapper.Build(
                    book.DiscussionPosts.Where(post =>
                        post.BookDiscussionThreadId == thread.Id),
                    book.Club.Slug,
                    userId,
                    isAdmin,
                    isAdmin || isMember)
            }));
        var activeThreadId = requestedThreadId is int requested &&
            threadItems.Any(thread => thread.Id == requested)
                ? requested
                : 0;

        return new ClubBookDetailsViewModel
        {
            Id = book.Id,
            ClubName = book.Club.Name,
            ClubSlug = book.Club.Slug,
            Title = book.Title,
            AuthorName = book.AuthorName,
            Notes = book.Notes,
            ImagePath = book.ImagePath,
            ReadingDate = book.ReadingDate,
            Status = ClubBookTimeline.Status(book, currentBook, now),
            CanPost = isAdmin || isMember,
            IsAdmin = isAdmin,
            IsAuthenticated = User.Identity?.IsAuthenticated == true,
            ActiveThreadId = activeThreadId,
            DiscussionThreads = threadItems
        };
    }

    private async Task<ImageValidationResult?> ValidateCoverAsync(
        IFormFile? cover,
        CancellationToken cancellationToken)
    {
        if (cover is null)
        {
            return null;
        }

        var result = await ImageUploadValidator.ValidateAsync(
            cover,
            cancellationToken);
        if (!result.IsValid)
        {
            ModelState.AddModelError(
                nameof(AddClubBookViewModel.CoverImage),
                result.ErrorMessage!);
        }

        return result;
    }

    private async Task<StoredImage> UploadCoverAsync(
        IFormFile cover,
        ImageValidationResult validation,
        CancellationToken cancellationToken)
    {
        await using var stream = cover.OpenReadStream();
        return await _imageStorage.UploadAsync(
            stream,
            validation.ContentType!,
            validation.FileExtension!,
            cancellationToken);
    }

    private async Task DeleteCoverBestEffortAsync(string objectKey)
    {
        try
        {
            await _imageStorage.DeleteAsync(
                objectKey,
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to delete book cover {ObjectKey}.",
                objectKey);
        }
    }
}
