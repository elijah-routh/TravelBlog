using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelBlog.Web.Data;
using TravelBlog.Web.Models;

namespace TravelBlog.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly BlogDbContext _context;

    public HomeController(
        ILogger<HomeController> logger,
        BlogDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        const int postsPerCategory = 8;

        var parodyEditorials = await _context.Posts
            .AsNoTracking()
            .Where(post =>
                post.IsPublished &&
                post.Category == PostCategory.ParodyEditorial)
            .OrderByDescending(post => post.CreatedAt)
            .Take(postsPerCategory)
            .ToListAsync();

        var shortStories = await _context.Posts
            .AsNoTracking()
            .Where(post =>
                post.IsPublished &&
                post.Category == PostCategory.ShortStory)
            .OrderByDescending(post => post.CreatedAt)
            .Take(postsPerCategory)
            .ToListAsync();

        var realNews = await _context.Posts
            .AsNoTracking()
            .Where(post =>
                post.IsPublished &&
                post.Category == PostCategory.RealNews)
            .OrderByDescending(post => post.CreatedAt)
            .Take(postsPerCategory)
            .ToListAsync();

        var viewModel = new HomeIndexViewModel
        {
            Categories =
            [
                new EditorialCategoryViewModel
                {
                    Name = "Parody Editorial",
                    Slug = "parody-editorial",
                    Posts = parodyEditorials
                },
                new EditorialCategoryViewModel
                {
                    Name = "Short Stories",
                    Slug = "short-stories",
                    Posts = shortStories
                },
                new EditorialCategoryViewModel
                {
                    Name = "Real News",
                    Slug = "real-news",
                    Posts = realNews
                }
            ]
        };

        return View(viewModel);
    }

    public IActionResult Privacy()
    {
        return View();
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
}