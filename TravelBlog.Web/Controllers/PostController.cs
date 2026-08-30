using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    private readonly IImageUploadRateLimiter _imageUploadRateLimiter;
    private readonly ILogger<PostsController> _logger;

    public PostsController(
        BlogDbContext context,
        IAuthorizationService authorizationService,
        UserManager<ApplicationUser> userManager,
        IImageStorage imageStorage,
        IImageUploadRateLimiter imageUploadRateLimiter,
        ILogger<PostsController> logger)
    {
        _context = context;
        _authorizationService = authorizationService;
        _userManager = userManager;
        _imageStorage = imageStorage;
        _imageUploadRateLimiter = imageUploadRateLimiter;
        _logger = logger;
    }

    // GET: /Posts
    [AllowAnonymous]
    public async Task<IActionResult> Index(
        string? scope,
        string? sort,
        string? status,
        string? gallery)
    {
        var normalizedScope = PostListScope.Normalize(scope);
        if (normalizedScope == PostListScope.Mine &&
            User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        var normalizedSort = PostSortOrder.Normalize(sort);
        var normalizedStatus = PostPublishFilter.Normalize(status);
        var isCompactGallery = PostGallerySize.IsCompact(gallery);

        var query = _context.Posts
            .AsNoTracking()
            .Include(post => post.Author)
            .AsQueryable();

        if (normalizedScope == PostListScope.Mine)
        {
            var userId = _userManager.GetUserId(User);
            query = query.Where(post => post.AuthorId == userId);
            query = normalizedStatus switch
            {
                PostPublishFilter.Unpublished =>
                    query.Where(post => !post.IsPublished),
                PostPublishFilter.Both => query,
                _ => query.Where(post => post.IsPublished)
            };
        }
        else
        {
            query = query.Where(post => post.IsPublished);
        }

        query = normalizedSort == PostSortOrder.Oldest
            ? query.OrderBy(post => post.CreatedAt)
            : query.OrderByDescending(post => post.CreatedAt);

        var posts = await query.ToListAsync();
        var currentUser = await _userManager.GetUserAsync(User);

        return View(new PostsIndexViewModel
        {
            Posts = posts,
            Scope = normalizedScope,
            Sort = normalizedSort,
            Status = normalizedScope == PostListScope.Mine
                ? normalizedStatus
                : PostPublishFilter.Published,
            ShowUnpublished = normalizedScope == PostListScope.Mine &&
                normalizedStatus != PostPublishFilter.Published,
            IsCompactGallery = isCompactGallery,
            IsAuthenticated = User.Identity?.IsAuthenticated == true,
            CanWrite = currentUser?.EmailConfirmed == true
        });
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
            return View(await BuildPostDetailsViewModelAsync(post));
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
            ? View(await BuildPostDetailsViewModelAsync(post))
            : Forbid();
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicyNames.Comments)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(
        string slug,
        [Bind(Prefix = "NewComment")] AddPostCommentViewModel model)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var post = await FindVisiblePostAsync(slug);
        if (post is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(nameof(Details), await BuildPostDetailsViewModelAsync(post, model));
        }

        _context.PostComments.Add(new PostComment
        {
            PostId = post.Id,
            AuthorId = userId,
            Body = model.Body.Trim(),
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        TempData["StatusMessage"] = "Comment posted.";
        return RedirectToAction(nameof(Details), new { slug = post.Slug });
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicyNames.Comments)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplyComment(
        int id,
        AddPostCommentViewModel model)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var parent = await _context.PostComments
            .AsNoTracking()
            .Include(comment => comment.Post)
            .FirstOrDefaultAsync(comment => comment.Id == id);
        if (parent is null)
        {
            return NotFound();
        }

        if (!parent.Post.IsPublished)
        {
            var authorizationResult =
                await _authorizationService.AuthorizeAsync(
                    User,
                    parent.Post,
                    PolicyNames.PostOwnerOrAdmin);
            if (!authorizationResult.Succeeded)
            {
                return Forbid();
            }
        }

        if (parent.ParentId is not null)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "A reply is required.";
            return RedirectToAction(
                nameof(Details),
                new { slug = parent.Post.Slug });
        }

        _context.PostComments.Add(new PostComment
        {
            PostId = parent.PostId,
            ParentId = parent.Id,
            AuthorId = userId,
            Body = model.Body.Trim(),
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        TempData["StatusMessage"] = "Reply posted.";
        return RedirectToAction(
            nameof(Details),
            new { slug = parent.Post.Slug });
    }

    [HttpGet]
    public async Task<IActionResult> EditComment(int id)
    {
        var comment = await _context.PostComments
            .AsNoTracking()
            .Include(existing => existing.Post)
            .FirstOrDefaultAsync(existing => existing.Id == id);
        if (comment is null)
        {
            return NotFound();
        }

        if (!CanManageComment(comment))
        {
            return Forbid();
        }

        return View(new EditPostCommentViewModel
        {
            Id = comment.Id,
            PostSlug = comment.Post.Slug,
            Body = comment.Body
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditComment(
        int id,
        EditPostCommentViewModel model)
    {
        var comment = await _context.PostComments
            .Include(existing => existing.Post)
            .FirstOrDefaultAsync(existing => existing.Id == id);
        if (comment is null)
        {
            return NotFound();
        }

        if (!CanManageComment(comment))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            model.Id = id;
            model.PostSlug = comment.Post.Slug;
            return View(model);
        }

        comment.Body = model.Body.Trim();
        comment.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["StatusMessage"] = "Comment updated.";
        return RedirectToAction(nameof(Details), new { slug = comment.Post.Slug });
    }

    [HttpGet]
    public async Task<IActionResult> DeleteComment(int id)
    {
        var comment = await _context.PostComments
            .AsNoTracking()
            .Include(existing => existing.Author)
            .Include(existing => existing.Post)
            .FirstOrDefaultAsync(existing => existing.Id == id);
        if (comment is null)
        {
            return NotFound();
        }

        if (!CanManageComment(comment))
        {
            return Forbid();
        }

        return View(comment);
    }

    [HttpPost, ActionName("DeleteComment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCommentConfirmed(int id)
    {
        var comment = await _context.PostComments
            .Include(existing => existing.Post)
            .FirstOrDefaultAsync(existing => existing.Id == id);
        if (comment is null)
        {
            return NotFound();
        }

        if (!CanManageComment(comment))
        {
            return Forbid();
        }

        var slug = comment.Post.Slug;
        _context.PostComments.Remove(comment);
        await _context.SaveChangesAsync();

        TempData["StatusMessage"] = "Comment deleted.";
        return RedirectToAction(nameof(Details), new { slug });
    }

    // GET: /Posts/Create
    public async Task<IActionResult> Create()
    {
        var isVerified = (await _authorizationService.AuthorizeAsync(
            User,
            PolicyNames.VerifiedEmail)).Succeeded;
        ViewData["CanCreatePost"] = isVerified;
        return View(new CreatePostViewModel());
    }

    // POST: /Posts/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CreatePostViewModel model,
        CancellationToken cancellationToken)
    {
        ViewData["CanCreatePost"] = true;
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
            using var lease = _imageUploadRateLimiter.Acquire(HttpContext);
            if (!lease.IsAcquired)
            {
                return StatusCode(
                    StatusCodes.Status429TooManyRequests,
                    "Too many image uploads. Please try again later.");
            }

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
            using var lease = _imageUploadRateLimiter.Acquire(HttpContext);
            if (!lease.IsAcquired)
            {
                return StatusCode(
                    StatusCodes.Status429TooManyRequests,
                    "Too many image uploads. Please try again later.");
            }

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

    private async Task<Post?> FindVisiblePostAsync(string slug)
    {
        var post = await _context.Posts
            .AsNoTracking()
            .Include(existing => existing.Author)
            .FirstOrDefaultAsync(existing => existing.Slug == slug);
        if (post is null)
        {
            return null;
        }

        if (post.IsPublished)
        {
            return post;
        }

        var authorizationResult = await _authorizationService.AuthorizeAsync(
            User,
            post,
            PolicyNames.PostOwnerOrAdmin);
        return authorizationResult.Succeeded ? post : null;
    }

    private async Task<PostDetailsViewModel> BuildPostDetailsViewModelAsync(
        Post post,
        AddPostCommentViewModel? newComment = null)
    {
        var userId = _userManager.GetUserId(User);
        var currentUser = await _userManager.GetUserAsync(User);
        var isVerified = currentUser?.EmailConfirmed == true;
        var isAdmin = isVerified && User.IsInRole(RoleNames.Admin);
        var comments = await _context.PostComments
            .AsNoTracking()
            .Include(comment => comment.Author)
            .Where(comment => comment.PostId == post.Id)
            .OrderBy(comment => comment.CreatedAt)
            .ToListAsync();

        return new PostDetailsViewModel
        {
            Post = post,
            CanComment = isVerified,
            IsAuthenticated = User.Identity?.IsAuthenticated == true,
            CanManagePost = isVerified &&
                OwnerAccess.IsAdminOrOwner(
                    isAdmin,
                    userId,
                    post.AuthorId),
            NewComment = newComment ?? new AddPostCommentViewModel(),
            Comments = PostCommentThreadMapper.Build(
                comments,
                isVerified ? userId : null,
                isAdmin,
                canReply: isVerified)
        };
    }

    private bool CanManageComment(PostComment comment)
    {
        var userId = _userManager.GetUserId(User);
        return OwnerAccess.IsAdminOrOwner(
            User.IsInRole(RoleNames.Admin),
            userId,
            comment.AuthorId);
    }
}