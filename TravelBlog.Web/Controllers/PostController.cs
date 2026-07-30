using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelBlog.Web.Data;
using TravelBlog.Web.Models;

namespace TravelBlog.Web.Controllers;

public class PostsController : Controller
{
    private readonly BlogDbContext _context;

    public PostsController(BlogDbContext context)
    {
        _context = context;
    }

    // GET: /Posts
    public async Task<IActionResult> Index()
    {
        var posts = await _context.Posts
            .OrderByDescending(post => post.CreatedAt)
            .ToListAsync();

        return View(posts);
    }

    // GET: /Posts/Details/colca-canyon
    public async Task<IActionResult> Details(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        var post = await _context.Posts
            .FirstOrDefaultAsync(post => post.Slug == slug);

        if (post is null)
        {
            return NotFound();
        }

        return View(post);
    }

    // GET: /Posts/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: /Posts/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Post post)
    {
        var slugAlreadyExists = await _context.Posts
            .AnyAsync(existingPost =>
                existingPost.Slug == post.Slug
            );

        if (slugAlreadyExists)
        {
            ModelState.AddModelError(
                nameof(post.Slug),
                "That URL slug is already being used."
            );
        }

        if (!ModelState.IsValid)
        {
            return View(post);
        }

        post.CreatedAt = DateTime.UtcNow;

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

        var post = await _context.Posts.FindAsync(id);

        if (post is null)
        {
            return NotFound();
        }

        return View(post);
    }

    // POST: /Posts/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Post formPost)
    {
        if (id != formPost.Id)
        {
            return BadRequest();
        }

        var existingPost = await _context.Posts.FindAsync(id);

        if (existingPost is null)
        {
            return NotFound();
        }

        var slugAlreadyExists = await _context.Posts
            .AnyAsync(post =>
                post.Slug == formPost.Slug &&
                post.Id != formPost.Id
            );

        if (slugAlreadyExists)
        {
            ModelState.AddModelError(
                nameof(formPost.Slug),
                "That URL slug is already being used."
            );
        }

        if (!ModelState.IsValid)
        {
            return View(formPost);
        }

        existingPost.Title = formPost.Title;
        existingPost.Slug = formPost.Slug;
        existingPost.Summary = formPost.Summary;
        existingPost.Content = formPost.Content;
        existingPost.ImagePath = formPost.ImagePath;
        existingPost.Category = formPost.Category;
        existingPost.IsPublished = formPost.IsPublished;
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

        var post = await _context.Posts.FindAsync(id);

        if (post is null)
        {
            return NotFound();
        }

        return View(post);
    }

    // POST: /Posts/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var post = await _context.Posts.FindAsync(id);

        if (post is null)
        {
            return NotFound();
        }

        _context.Posts.Remove(post);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }
}