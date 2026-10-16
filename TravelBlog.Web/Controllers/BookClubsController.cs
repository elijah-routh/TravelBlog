using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelBlog.Web.Authorization;
using TravelBlog.Web.Data;
using TravelBlog.Web.Models;
using TravelBlog.Web.Services;

namespace TravelBlog.Web.Controllers;

[Route("BookClubs")]
public class BookClubsController : Controller
{
    private readonly BlogDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IImageStorage _imageStorage;
    private readonly ILogger<BookClubsController> _logger;

    public BookClubsController(
        BlogDbContext context,
        UserManager<ApplicationUser> userManager,
        IImageStorage imageStorage,
        ILogger<BookClubsController> logger)
    {
        _context = context;
        _userManager = userManager;
        _imageStorage = imageStorage;
        _logger = logger;
    }

    [HttpGet]
    [HttpGet("Index")]
    public async Task<IActionResult> Index() =>
        View(await BuildHomeViewModelAsync());

    [HttpGet("Clubs")]
    public IActionResult Clubs() => RedirectToAction(nameof(Index));

    [HttpGet("Timeline")]
    public async Task<IActionResult> Timeline()
    {
        var books = await _context.ClubBooks
            .AsNoTracking()
            .Include(book => book.Club)
            .OrderByDescending(book => book.EndDate)
            .ThenBy(book => book.Title)
            .ToListAsync();
        var now = DateTime.UtcNow;

        return View(new CombinedBookTimelineViewModel
        {
            Books = books.Select(book => new CombinedBookTimelineItemViewModel
            {
                Id = book.Id,
                ClubName = book.Club.Name,
                ClubSlug = book.Club.Slug,
                Title = book.Title,
                AuthorName = book.AuthorName,
                Notes = book.Notes,
                ImagePath = book.ImagePath,
                StartDate = book.StartDate,
                EndDate = book.EndDate,
                Status = ClubBookTimeline.Status(book, now)
            }).ToList()
        });
    }

    [HttpGet("Create")]
    [Authorize(Roles = RoleNames.Admin)]
    public IActionResult Create()
    {
        return View(new BookClubFormViewModel());
    }

    [HttpPost("Create")]
    [Authorize(Roles = RoleNames.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BookClubFormViewModel model)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        if (await _context.BookClubs.AnyAsync(club => club.Slug == model.Slug))
        {
            ModelState.AddModelError(
                nameof(model.Slug),
                "That URL slug is already being used.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var club = new BookClub
        {
            Name = model.Name.Trim(),
            Slug = model.Slug,
            Description = string.IsNullOrWhiteSpace(model.Description)
                ? null
                : model.Description.Trim(),
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow,
            Memberships =
            [
                new BookClubMembership
                {
                    UserId = userId,
                    JoinedAt = DateTime.UtcNow
                }
            ]
        };

        _context.BookClubs.Add(club);
        await _context.SaveChangesAsync();

        TempData["StatusMessage"] = $"{club.Name} is ready.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{slug}/Edit")]
    [Authorize(Roles = RoleNames.Admin)]
    public IActionResult Edit(string slug) =>
        RedirectToAction(nameof(Index));

    [HttpPost("{slug}/Edit")]
    [Authorize(Roles = RoleNames.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string slug, BookClubFormViewModel model)
    {
        var club = await _context.BookClubs
            .FirstOrDefaultAsync(existing => existing.Slug == slug);
        if (club is null)
        {
            return NotFound();
        }

        var slugTaken = await _context.BookClubs.AnyAsync(existing =>
            existing.Slug == model.Slug && existing.Id != club.Id);
        if (slugTaken)
        {
            ModelState.AddModelError(
                nameof(model.Slug),
                "That URL slug is already being used.");
        }

        if (!ModelState.IsValid)
        {
            ViewData["ClubSlug"] = slug;
            return View(model);
        }

        club.Name = model.Name.Trim();
        club.Slug = model.Slug;
        club.Description = string.IsNullOrWhiteSpace(model.Description)
            ? null
            : model.Description.Trim();

        await _context.SaveChangesAsync();

        TempData["StatusMessage"] = "Club details were updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{slug}/Delete")]
    [Authorize(Roles = RoleNames.Admin)]
    public IActionResult Delete(string slug) =>
        RedirectToAction(nameof(Index));

    [HttpPost("{slug}/Delete")]
    [Authorize(Roles = RoleNames.Admin)]
    [ValidateAntiForgeryToken]
    [ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(string slug)
    {
        var club = await _context.BookClubs
            .Include(existing => existing.Books)
            .FirstOrDefaultAsync(existing => existing.Slug == slug);
        if (club is null)
        {
            return NotFound();
        }

        var objectKeys = club.Books
            .Select(book => book.ImageObjectKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Cast<string>()
            .ToList();
        _context.BookClubs.Remove(club);
        await _context.SaveChangesAsync();

        foreach (var objectKey in objectKeys)
        {
            await DeleteCoverBestEffortAsync(objectKey);
        }

        TempData["StatusMessage"] = $"{club.Name} was deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{slug}")]
    public IActionResult Details(string slug) =>
        RedirectToAction(nameof(Index));

    [HttpPost("{slug}/Join")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Join(string slug, string? returnUrl)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var club = await _context.BookClubs
            .Include(existing => existing.Memberships)
            .FirstOrDefaultAsync(existing => existing.Slug == slug);
        if (club is null)
        {
            return NotFound();
        }

        if (club.Memberships.All(membership => membership.UserId != userId))
        {
            club.Memberships.Add(new BookClubMembership
            {
                UserId = userId,
                JoinedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            TempData["StatusMessage"] = $"You joined {club.Name}.";
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{slug}/Leave")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Leave(string slug)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var club = await _context.BookClubs
            .Include(existing => existing.Memberships)
            .FirstOrDefaultAsync(existing => existing.Slug == slug);
        if (club is null)
        {
            return NotFound();
        }

        var membership = club.Memberships
            .FirstOrDefault(existing => existing.UserId == userId);
        if (membership is not null)
        {
            club.Memberships.Remove(membership);
            await _context.SaveChangesAsync();
            TempData["StatusMessage"] = $"You left {club.Name}.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Notices")]
    [Authorize(Roles = RoleNames.Admin)]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AddHomeNotice(
        string clubSlug,
        [Bind(Prefix = "NewNotice")] AddClubNoticeViewModel model) =>
        AddNotice(clubSlug, model, home: true);

    [HttpPost("{slug}/Notices")]
    [Authorize(Roles = RoleNames.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddNotice(
        string slug,
        [Bind(Prefix = "NewNotice")] AddClubNoticeViewModel model,
        bool home = false)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var club = await _context.BookClubs
            .FirstOrDefaultAsync(existing => existing.Slug == slug);
        if (club is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            if (home)
            {
                var homeView = await BuildHomeViewModelAsync();
                homeView.NewNotice = model;
                return View(nameof(Index), homeView);
            }

            var invalidView = await BuildDetailsViewModelAsync(slug);
            if (invalidView is null)
            {
                return NotFound();
            }

            invalidView.NewNotice = model;
            return View(nameof(Details), invalidView);
        }

        _context.ClubNotices.Add(new ClubNotice
        {
            ClubId = club.Id,
            Body = model.Body.Trim(),
            AuthorId = userId,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        TempData["StatusMessage"] = "Notice posted.";
        return NoticeRedirect(home, slug);
    }

    [HttpPost("{slug}/Notices/{noticeId:int}/Edit")]
    [Authorize(Roles = RoleNames.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditNotice(
        string slug,
        int noticeId,
        AddClubNoticeViewModel model,
        bool home = false)
    {
        var notice = await _context.ClubNotices
            .Include(existing => existing.Club)
            .FirstOrDefaultAsync(existing =>
                existing.Id == noticeId &&
                existing.Club.Slug == slug);
        if (notice is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] =
                ModelState.Values
                    .SelectMany(value => value.Errors)
                    .Select(error => error.ErrorMessage)
                    .FirstOrDefault()
                ?? "Enter a valid notice.";
            return NoticeRedirect(home, slug);
        }

        notice.Body = model.Body.Trim();
        await _context.SaveChangesAsync();

        TempData["StatusMessage"] = "Notice updated.";
        return NoticeRedirect(home, slug);
    }

    [HttpPost("{slug}/Notices/{noticeId:int}/Delete")]
    [Authorize(Roles = RoleNames.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteNotice(
        string slug,
        int noticeId,
        bool home = false)
    {
        var notice = await _context.ClubNotices
            .Include(existing => existing.Club)
            .FirstOrDefaultAsync(existing =>
                existing.Id == noticeId &&
                existing.Club.Slug == slug);
        if (notice is null)
        {
            return NotFound();
        }

        _context.ClubNotices.Remove(notice);
        await _context.SaveChangesAsync();

        TempData["StatusMessage"] = "Notice deleted.";
        return NoticeRedirect(home, slug);
    }

    private IActionResult NoticeRedirect(bool home, string slug) =>
        home
            ? RedirectToAction(nameof(Index))
            : RedirectToAction(nameof(Details), new { slug });

    [HttpPost("{slug}/Discussions")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddDiscussion(
        string slug,
        string? sort,
        [Bind(Prefix = "NewDiscussion")] AddDiscussionPostViewModel model)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var club = await _context.BookClubs
            .FirstOrDefaultAsync(existing => existing.Slug == slug);
        if (club is null)
        {
            return NotFound();
        }

        if (!await CanPostAsync(club.Id, userId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            var invalidView = await BuildDetailsViewModelAsync(
                slug,
                discussionSort: sort);
            if (invalidView is null)
            {
                return NotFound();
            }

            invalidView.NewDiscussion = model;
            return View(nameof(Details), invalidView);
        }

        _context.DiscussionPosts.Add(new DiscussionPost
        {
            ClubId = club.Id,
            ClubBookId = null,
            Body = model.Body.Trim(),
            AuthorId = userId,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        TempData["StatusMessage"] = "Message posted.";
        return RedirectToAction(
            nameof(Details),
            new { slug, sort = DiscussionSortOrder.Normalize(sort) });
    }

    private async Task<BookClubHomeViewModel> BuildHomeViewModelAsync()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var canWrite = currentUser?.EmailConfirmed == true;
        var now = DateTime.UtcNow;
        var clubs = await _context.BookClubs
            .AsNoTracking()
            .Include(club => club.Books)
            .OrderBy(club => club.Name)
            .ToListAsync();
        var notices = await _context.ClubNotices
            .AsNoTracking()
            .Include(notice => notice.Author)
            .Include(notice => notice.Club)
            .OrderByDescending(notice => notice.CreatedAt)
            .ToListAsync();

        var books = clubs
            .SelectMany(club => club.Books.Select(book => ToTimelineItem(
                book,
                club,
                now)))
            .ToList();
        var featured = books
            .Where(book => book.Status == ClubBookTimeline.Current)
            .OrderBy(book => book.EndDate)
            .ThenBy(book => book.Id)
            .FirstOrDefault()
            ?? books
                .Where(book => book.Status == ClubBookTimeline.Upcoming)
                .OrderBy(book => book.EndDate)
                .ThenBy(book => book.Id)
                .FirstOrDefault()
            ?? books
                .OrderByDescending(book => book.EndDate)
                .ThenByDescending(book => book.Id)
                .FirstOrDefault();
        var primaryClubSlug = featured?.ClubSlug
            ?? (clubs.Count == 1 ? clubs[0].Slug : null);

        return new BookClubHomeViewModel
        {
            IsAdmin = canWrite && User.IsInRole(RoleNames.Admin),
            ShowClubNames = false,
            PrimaryClubSlug = primaryClubSlug,
            FeaturedBook = featured,
            UpcomingBooks = books
                .Where(book =>
                    book.Status == ClubBookTimeline.Current ||
                    book.Status == ClubBookTimeline.Upcoming)
                .OrderBy(book => book.Status == ClubBookTimeline.Current ? 0 : 1)
                .ThenBy(book => book.StartDate)
                .ThenBy(book => book.EndDate)
                .ThenBy(book => book.Title, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            PastBooks = books
                .Where(book => book.EndDate.Date < now.Date)
                .OrderByDescending(book => book.EndDate)
                .ThenBy(book => book.Title, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Notices = notices
                .Select(notice => new ClubNoticeItemViewModel
                {
                    Id = notice.Id,
                    ClubName = notice.Club.Name,
                    ClubSlug = notice.Club.Slug,
                    AuthorDisplayName = notice.Author.DisplayName,
                    Body = notice.Body,
                    CreatedAt = notice.CreatedAt
                })
                .ToList(),
            NoticeClubs = clubs
                .Select(club => new BookClubNoticeTargetViewModel
                {
                    Name = club.Name,
                    Slug = club.Slug
                })
                .ToList()
        };
    }

    private static CombinedBookTimelineItemViewModel ToTimelineItem(
        ClubBook book,
        BookClub club,
        DateTime utcNow) =>
        new()
        {
            Id = book.Id,
            ClubName = club.Name,
            ClubSlug = club.Slug,
            Title = book.Title,
            AuthorName = book.AuthorName,
            Notes = book.Notes,
            ImagePath = book.ImagePath,
            StartDate = book.StartDate,
            EndDate = book.EndDate,
            Status = ClubBookTimeline.Status(book, utcNow)
        };

    private async Task<BookClub?> FindClubBySlugAsync(string slug) =>
        await _context.BookClubs
            .AsNoTracking()
            .FirstOrDefaultAsync(club => club.Slug == slug);

    private async Task<bool> CanPostAsync(int clubId, string userId)
    {
        if (User.IsInRole(RoleNames.Admin))
        {
            return true;
        }

        return await _context.BookClubMemberships.AnyAsync(membership =>
            membership.ClubId == clubId && membership.UserId == userId);
    }

    private async Task<BookClubDetailsViewModel?> BuildDetailsViewModelAsync(
        string slug,
        bool showTimeline = false,
        string? discussionSort = null)
    {
        var club = await _context.BookClubs
            .AsNoTracking()
            .Include(existing => existing.Memberships)
            .Include(existing => existing.Books)
            .Include(existing => existing.Notices)
                .ThenInclude(notice => notice.Author)
            .Include(existing => existing.DiscussionPosts
                .Where(post => post.ClubBookId == null))
                .ThenInclude(post => post.Author)
            .Include(existing => existing.DiscussionPosts
                .Where(post => post.ClubBookId == null))
                .ThenInclude(post => post.Likes)
            .Include(existing => existing.DiscussionPosts
                .Where(post => post.ClubBookId == null))
                .ThenInclude(post => post.Poll)
                    .ThenInclude(poll => poll!.Options)
                        .ThenInclude(option => option.Votes)
                            .ThenInclude(vote => vote.User)
            .FirstOrDefaultAsync(existing => existing.Slug == slug);

        if (club is null)
        {
            return null;
        }

        var userId = _userManager.GetUserId(User);
        var currentUser = await _userManager.GetUserAsync(User);
        var isVerified = currentUser?.EmailConfirmed == true;
        var isAdmin = isVerified && User.IsInRole(RoleNames.Admin);
        var isMember = !string.IsNullOrWhiteSpace(userId) &&
            club.Memberships.Any(membership => membership.UserId == userId);
        var memberDisplayNames = (await _context.BookClubMemberships
                .AsNoTracking()
                .Where(membership => membership.ClubId == club.Id)
                .Select(membership => membership.User.DisplayName)
                .ToListAsync())
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(name => name, StringComparer.Ordinal)
            .ToList();
        var now = DateTime.UtcNow;
        var normalizedSort = DiscussionSortOrder.Normalize(discussionSort);
        var currentBook = ClubBookTimeline.CurrentBook(club.Books, now);
        var bookItems = club.Books
            .OrderBy(book => book.EndDate)
            .ThenBy(book => book.Id)
            .Select(book => new ClubBookTimelineItemViewModel
            {
                Id = book.Id,
                Title = book.Title,
                AuthorName = book.AuthorName,
                Notes = book.Notes,
                ImagePath = book.ImagePath,
                StartDate = book.StartDate,
                EndDate = book.EndDate,
                Status = ClubBookTimeline.Status(book, now)
            })
            .ToList();

        return new BookClubDetailsViewModel
        {
            Id = club.Id,
            Name = club.Name,
            Slug = club.Slug,
            Description = club.Description,
            MemberCount = memberDisplayNames.Count,
            MemberDisplayNames = memberDisplayNames,
            IsMember = isMember,
            CanPost = isVerified && (isAdmin || isMember),
            IsAdmin = isAdmin,
            IsVerified = isVerified,
            ShowTimeline = showTimeline,
            DiscussionSort = normalizedSort,
            CurrentBook = bookItems.FirstOrDefault(book =>
                book.Id == currentBook?.Id),
            Notices = club.Notices
                .OrderByDescending(notice => notice.CreatedAt)
                .Select(notice => new ClubNoticeItemViewModel
                {
                    Id = notice.Id,
                    AuthorDisplayName = notice.Author.DisplayName,
                    Body = notice.Body,
                    CreatedAt = notice.CreatedAt
                })
                .ToList(),
            Books = bookItems,
            DiscussionPosts = DiscussionThreadMapper.Build(
                club.DiscussionPosts.Where(post => post.ClubBookId is null),
                club.Slug,
                userId,
                isAdmin,
                isVerified && (isAdmin || isMember),
                normalizedSort)
        };
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
