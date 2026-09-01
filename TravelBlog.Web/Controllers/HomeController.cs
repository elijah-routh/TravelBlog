using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelBlog.Web.Authorization;
using TravelBlog.Web.Data;
using TravelBlog.Web.Models;
using TravelBlog.Web.Services;

namespace TravelBlog.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly BlogDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TimeProvider _timeProvider;

    public HomeController(
        ILogger<HomeController> logger,
        BlogDbContext context,
        UserManager<ApplicationUser> userManager,
        TimeProvider timeProvider)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
        _timeProvider = timeProvider;
    }

    public async Task<IActionResult> Index()
    {
        const int postsPerCategory = 8;

        var literatureAndStuff = await _context.Posts
            .AsNoTracking()
            .Include(post => post.Author)
            .ExcludeContact()
            .PubliclyListed()
            .Where(post =>
                post.Category == PostCategory.LiteratureAndStuff)
            .OrderByDescending(post => post.CreatedAt)
            .Take(postsPerCategory)
            .ToListAsync();

        var humorAndSatire = await _context.Posts
            .AsNoTracking()
            .Include(post => post.Author)
            .ExcludeContact()
            .PubliclyListed()
            .Where(post =>
                post.Category == PostCategory.FictionAndSatire)
            .OrderByDescending(post => post.CreatedAt)
            .Take(postsPerCategory)
            .ToListAsync();

        var other = await _context.Posts
            .AsNoTracking()
            .Include(post => post.Author)
            .ExcludeContact()
            .PubliclyListed()
            .Where(post =>
                post.Category == PostCategory.Other)
            .OrderByDescending(post => post.CreatedAt)
            .Take(postsPerCategory)
            .ToListAsync();

        var viewModel = new HomeIndexViewModel
        {
            Categories =
            [
                new EditorialCategoryViewModel
                {
                    Name = "Literature and Stuff",
                    Slug = "literature-and-stuff",
                    Posts = literatureAndStuff
                },
                new EditorialCategoryViewModel
                {
                    Name = "Humor and Satire",
                    Slug = "humor-and-satire",
                    Posts = humorAndSatire
                },
                new EditorialCategoryViewModel
                {
                    Name = "Other",
                    Slug = "other",
                    Posts = other
                }
            ]
        };

        return View(viewModel);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Faq()
    {
        return View(new FaqViewModel
        {
            Items = SiteFaq.Items
        });
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Contact()
    {
        return View(await BuildContactFormAsync());
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Contact(CreateContactPostViewModel model)
    {
        var form = await BuildContactFormAsync();
        model.CanSubmit = form.CanSubmit;
        model.HasReachedDailyLimit = form.HasReachedDailyLimit;
        model.IsBlocked = form.IsBlocked;

        if (!model.CanSubmit)
        {
            if (model.IsBlocked)
            {
                return View(model);
            }

            if (model.HasReachedDailyLimit)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "You can only send one contact message per day.");
            }

            return View(model);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var slug = ContactPostSlug.Create(model.Title);
        while (await _context.Posts.AnyAsync(post => post.Slug == slug))
        {
            slug = ContactPostSlug.Create(model.Title);
        }

        _context.Posts.Add(new Post
        {
            AuthorId = userId,
            Title = model.Title.Trim(),
            Slug = slug,
            Content = model.Content.Trim(),
            Category = PostCategory.Contact,
            IsPublished = false,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        });
        await _context.SaveChangesAsync();

        return View(new CreateContactPostViewModel
        {
            Submitted = true,
            CanSubmit = true
        });
    }

    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id
                ?? HttpContext.TraceIdentifier
        });
    }

    private async Task<CreateContactPostViewModel> BuildContactFormAsync()
    {
        var model = new CreateContactPostViewModel();
        var user = await _userManager.GetUserAsync(User);
        if (user?.EmailConfirmed != true)
        {
            return model;
        }

        model.IsBlocked = user.IsBlocked;
        if (user.IsBlocked)
        {
            return model;
        }

        var userId = user.Id;
        var dayStart = _timeProvider.GetUtcNow().UtcDateTime.Date;
        model.HasReachedDailyLimit = await _context.Posts.AnyAsync(post =>
            post.AuthorId == userId &&
            post.Category == PostCategory.Contact &&
            post.CreatedAt >= dayStart);
        model.CanSubmit = !model.HasReachedDailyLimit;

        return model;
    }
}
