using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelBlog.Web.Authorization;
using TravelBlog.Web.Data;
using TravelBlog.Web.Models;

namespace TravelBlog.Web.Controllers;

[Authorize]
public class PostsController : Controller
{
    private readonly BlogDbContext _context;
    private readonly IAuthorizationService _authorizationService;
    private readonly UserManager<ApplicationUser> _userManager;

    public PostsController(
        BlogDbContext context,
        IAuthorizationService authorizationService,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _authorizationService = authorizationService;
        _userManager = userManager;
    }

    // GET: /Posts
    public async Task<IActionResult> Index()
    {
        var query = _context.Posts
            .AsNoTracking()
            .Include(post => post.Author)
            .AsQueryable();

        if (!User.IsInRole(RoleNames.Admin))
        {
            var userId = _userManager.GetUserId(User);
            query = query.Where(post => post.AuthorId == userId);
        }

        var posts = await query
            .OrderByDescending(post => post.CreatedAt)
            .ToListAsync();

        return View(posts);
    }

    // GET: /Posts/Details/colca-canyon
    [AllowAnonymous]
    public async Task<IActionResult> Details(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        var post = await _context.Posts
            .AsNoTracking()
            .Include(post => post.Author)
            .FirstOrDefaultAsync(post => post.Slug == slug);

        if (post is null)
        {
            return NotFound();
        }

        if (post.IsPublished)
        {
            return View(post);
        }

        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        var authorizationResult =
            await _authorizationService.AuthorizeAsync(
                User,
                post,
                PolicyNames.PostOwnerOrAdmin);

        return authorizationResult.Succeeded
            ? View(post)
            : Forbid();
    }

    // GET: /Posts/Create
    public IActionResult Create()
    {
        return View(new CreatePostViewModel());
    }

    // POST: /Posts/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CreatePostViewModel model)
    {
        var slugAlreadyExists = await _context.Posts
            .AnyAsync(existingPost =>
                existingPost.Slug == model.Slug
            );

        if (slugAlreadyExists)
        {
            ModelState.AddModelError(
                nameof(model.Slug),
                "That URL slug is already being used."
            );
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

        var post = new Post
        {
            Title = model.Title,
            Slug = model.Slug,
            Summary = model.Summary,
            Content = model.Content,
            ImagePath = model.ImagePath,
            Category = model.Category,
            IsPublished = model.IsPublished,
            AuthorId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // GET: /Posts/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var post = await _context.Posts
            .AsNoTracking()
            .Include(post => post.Author)
            .FirstOrDefaultAsync(post => post.Id == id);

        if (post is null)
        {
            return NotFound();
        }

        var authorizationResult =
            await _authorizationService.AuthorizeAsync(
                User,
                post,
                PolicyNames.PostOwnerOrAdmin);

        if (!authorizationResult.Succeeded)
        {
            return Forbid();
        }

        return View(new EditPostViewModel
        {
            Id = post.Id,
            Title = post.Title,
            Slug = post.Slug,
            Summary = post.Summary,
            Content = post.Content,
            ImagePath = post.ImagePath,
            Category = post.Category,
            IsPublished = post.IsPublished
        });
    }

    // POST: /Posts/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        EditPostViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var existingPost = await _context.Posts
            .Include(post => post.Author)
            .FirstOrDefaultAsync(post => post.Id == id);

        if (existingPost is null)
        {
            return NotFound();
        }

        var authorizationResult =
            await _authorizationService.AuthorizeAsync(
                User,
                existingPost,
                PolicyNames.PostOwnerOrAdmin);

        if (!authorizationResult.Succeeded)
        {
            return Forbid();
        }

        var slugAlreadyExists = await _context.Posts
            .AnyAsync(post =>
                post.Slug == model.Slug &&
                post.Id != model.Id
            );

        if (slugAlreadyExists)
        {
            ModelState.AddModelError(
                nameof(model.Slug),
                "That URL slug is already being used."
            );
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        existingPost.Title = model.Title;
        existingPost.Slug = model.Slug;
        existingPost.Summary = model.Summary;
        existingPost.Content = model.Content;
        existingPost.ImagePath = model.ImagePath;
        existingPost.Category = model.Category;
        existingPost.IsPublished = model.IsPublished;
        existingPost.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // GET: /Posts/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var post = await _context.Posts
            .AsNoTracking()
            .Include(post => post.Author)
            .FirstOrDefaultAsync(post => post.Id == id);

        if (post is null)
        {
            return NotFound();
        }

        var authorizationResult =
            await _authorizationService.AuthorizeAsync(
                User,
                post,
                PolicyNames.PostOwnerOrAdmin);

        return authorizationResult.Succeeded
            ? View(post)
            : Forbid();
    }

    // POST: /Posts/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var post = await _context.Posts
            .Include(post => post.Author)
            .FirstOrDefaultAsync(post => post.Id == id);

        if (post is null)
        {
            return NotFound();
        }

        var authorizationResult =
            await _authorizationService.AuthorizeAsync(
                User,
                post,
                PolicyNames.PostOwnerOrAdmin);

        if (!authorizationResult.Succeeded)
        {
            return Forbid();
        }

        _context.Posts.Remove(post);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }
}