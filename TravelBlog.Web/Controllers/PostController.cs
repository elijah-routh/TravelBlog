using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelBlog.Web.Authorization;
using TravelBlog.Web.Data;
using TravelBlog.Web.Models;
using TravelBlog.Web.Services;

namespace TravelBlog.Web.Controllers;

[Authorize]
public class PostsController : Controller
{
    private readonly BlogDbContext _context;
    private readonly IAuthorizationService _authorizationService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IImageStorage _imageStorage;
    private readonly ILogger<PostsController> _logger;

    public PostsController(
        BlogDbContext context,
        IAuthorizationService authorizationService,
        UserManager<ApplicationUser> userManager,
        IImageStorage imageStorage,
        ILogger<PostsController> logger)
    {
        _context = context;
        _authorizationService = authorizationService;
        _userManager = userManager;
        _imageStorage = imageStorage;
        _logger = logger;
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
        CreatePostViewModel model,
        CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var slugAlreadyExists = await _context.Posts
            .AnyAsync(
                existingPost => existingPost.Slug == model.Slug,
                cancellationToken);

        if (slugAlreadyExists)
        {
            ModelState.AddModelError(
                nameof(model.Slug),
                "That URL slug is already being used."
            );
        }

        ImageValidationResult? imageValidation = null;

        if (model.FeaturedImage is not null)
        {
            imageValidation = await ImageUploadValidator.ValidateAsync(
                model.FeaturedImage,
                cancellationToken);

            if (!imageValidation.IsValid)
            {
                ModelState.AddModelError(
                    nameof(model.FeaturedImage),
                    imageValidation.ErrorMessage!);
            }
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        StoredImage? uploadedImage = null;

        if (model.FeaturedImage is not null)
        {
            await using var imageStream =
                model.FeaturedImage.OpenReadStream();
            uploadedImage = await _imageStorage.UploadAsync(
                imageStream,
                imageValidation!.ContentType!,
                imageValidation.FileExtension!,
                cancellationToken);
        }

        var post = new Post
        {
            Title = model.Title,
            Slug = model.Slug,
            Summary = model.Summary,
            Content = model.Content,
            ImagePath = uploadedImage?.PublicUrl,
            ImageObjectKey = uploadedImage?.ObjectKey,
            Category = model.Category,
            IsPublished = model.IsPublished,
            AuthorId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Posts.Add(post);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (uploadedImage is not null)
            {
                await DeleteImageBestEffortAsync(
                    uploadedImage.ObjectKey,
                    "compensating for a failed post creation");
            }

            throw;
        }

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
            CurrentImagePath = post.ImagePath,
            Category = post.Category,
            IsPublished = post.IsPublished
        });
    }

    // POST: /Posts/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        EditPostViewModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var existingPost = await _context.Posts
            .Include(post => post.Author)
            .FirstOrDefaultAsync(
                post => post.Id == id,
                cancellationToken);

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

        model.CurrentImagePath = existingPost.ImagePath;

        var slugAlreadyExists = await _context.Posts
            .AnyAsync(
                post =>
                    post.Slug == model.Slug &&
                    post.Id != model.Id,
                cancellationToken);

        if (slugAlreadyExists)
        {
            ModelState.AddModelError(
                nameof(model.Slug),
                "That URL slug is already being used."
            );
        }

        if (model.FeaturedImage is not null &&
            model.RemoveFeaturedImage)
        {
            ModelState.AddModelError(
                nameof(model.FeaturedImage),
                "Choose a replacement image or remove the current image, not both.");
        }

        ImageValidationResult? imageValidation = null;

        if (model.FeaturedImage is not null)
        {
            imageValidation = await ImageUploadValidator.ValidateAsync(
                model.FeaturedImage,
                cancellationToken);

            if (!imageValidation.IsValid)
            {
                ModelState.AddModelError(
                    nameof(model.FeaturedImage),
                    imageValidation.ErrorMessage!);
            }
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var previousObjectKey = existingPost.ImageObjectKey;
        StoredImage? uploadedImage = null;

        if (model.FeaturedImage is not null)
        {
            await using var imageStream =
                model.FeaturedImage.OpenReadStream();
            uploadedImage = await _imageStorage.UploadAsync(
                imageStream,
                imageValidation!.ContentType!,
                imageValidation.FileExtension!,
                cancellationToken);
        }

        existingPost.Title = model.Title;
        existingPost.Slug = model.Slug;
        existingPost.Summary = model.Summary;
        existingPost.Content = model.Content;
        existingPost.Category = model.Category;
        existingPost.IsPublished = model.IsPublished;
        existingPost.UpdatedAt = DateTime.UtcNow;

        if (uploadedImage is not null)
        {
            existingPost.ImagePath = uploadedImage.PublicUrl;
            existingPost.ImageObjectKey = uploadedImage.ObjectKey;
        }
        else if (model.RemoveFeaturedImage)
        {
            existingPost.ImagePath = null;
            existingPost.ImageObjectKey = null;
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (uploadedImage is not null)
            {
                await DeleteImageBestEffortAsync(
                    uploadedImage.ObjectKey,
                    "compensating for a failed post update");
            }

            throw;
        }

        if (!string.IsNullOrWhiteSpace(previousObjectKey) &&
            (uploadedImage is not null || model.RemoveFeaturedImage))
        {
            await DeleteImageBestEffortAsync(
                previousObjectKey,
                "cleaning up a replaced or removed featured image");
        }

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
    public async Task<IActionResult> DeleteConfirmed(
        int id,
        CancellationToken cancellationToken)
    {
        var post = await _context.Posts
            .Include(post => post.Author)
            .FirstOrDefaultAsync(
                post => post.Id == id,
                cancellationToken);

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

        var objectKey = post.ImageObjectKey;

        _context.Posts.Remove(post);
        await _context.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(objectKey))
        {
            await DeleteImageBestEffortAsync(
                objectKey,
                "cleaning up a deleted post");
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task DeleteImageBestEffortAsync(
        string objectKey,
        string operation)
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
                "Failed to delete image object {ObjectKey} while {Operation}.",
                objectKey,
                operation);
        }
    }
}