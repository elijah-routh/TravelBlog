using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelBlog.Web.Data;
using TravelBlog.Web.Models;

namespace TravelBlog.Web.Controllers;

[ApiController]
[Route("api/posts")]
public class ApiPostsController : ControllerBase
{
    private const string ApiKeyConfigKey = "BlogPostApiKey";

    private readonly BlogDbContext _context;
    private readonly IConfiguration _configuration;

    public ApiPostsController(
        BlogDbContext context,
        IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpPost]
    public async Task<IActionResult> Upsert(ApiPostRequest request)
    {
        var authFailure = ValidateApiKey();
        if (authFailure is not null)
        {
            return authFailure;
        }

        var now = DateTime.UtcNow;
        var existingPost = await _context.Posts
            .FirstOrDefaultAsync(post => post.Slug == request.Slug);

        if (existingPost is null)
        {
            var post = new Post
            {
                Title = request.Title,
                Slug = request.Slug,
                Summary = request.Summary,
                Content = request.Content,
                Category = request.Category ?? PostCategory.RealNews,
                IsPublished = request.IsPublished ?? true,
                CreatedAt = request.CreatedAt ?? now
            };

            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                "Details",
                "Posts",
                new { slug = post.Slug },
                ApiPostResponse.FromPost(post, created: true));
        }

        existingPost.Title = request.Title;
        existingPost.Slug = request.Slug;
        existingPost.Summary = request.Summary;
        existingPost.Content = request.Content;
        existingPost.Category = request.Category ?? existingPost.Category;
        existingPost.IsPublished = request.IsPublished ?? existingPost.IsPublished;
        existingPost.UpdatedAt = now;

        await _context.SaveChangesAsync();

        return Ok(ApiPostResponse.FromPost(existingPost, created: false));
    }

    private IActionResult? ValidateApiKey()
    {
        var expectedApiKey = _configuration[ApiKeyConfigKey];
        if (string.IsNullOrWhiteSpace(expectedApiKey))
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { detail = $"{ApiKeyConfigKey} is not configured." });
        }

        var authorization = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization) ||
            !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Unauthorized(new { detail = "Missing bearer token." });
        }

        var suppliedToken = authorization["Bearer ".Length..].Trim();
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedToken);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedApiKey);

        if (!CryptographicOperations.FixedTimeEquals(
                suppliedBytes,
                expectedBytes))
        {
            return Unauthorized(new { detail = "Invalid API key." });
        }

        return null;
    }
}

public class ApiPostRequest
{
    [Required(ErrorMessage = "A title is required.")]
    [StringLength(
        150,
        ErrorMessage = "The title cannot exceed 150 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "A URL slug is required.")]
    [StringLength(
        160,
        ErrorMessage = "The slug cannot exceed 160 characters.")]
    [RegularExpression(
        @"^[a-z0-9]+(?:-[a-z0-9]+)*$",
        ErrorMessage = "Use lowercase letters, numbers, and hyphens only.")]
    public string Slug { get; set; } = string.Empty;

    [StringLength(
        350,
        ErrorMessage = "The summary cannot exceed 350 characters.")]
    public string? Summary { get; set; }

    [Required(ErrorMessage = "Post content is required.")]
    public string Content { get; set; } = string.Empty;

    public PostCategory? Category { get; set; }

    public bool? IsPublished { get; set; }

    public DateTime? CreatedAt { get; set; }
}

public class ApiPostResponse
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public bool Created { get; set; }

    public static ApiPostResponse FromPost(Post post, bool created)
    {
        return new ApiPostResponse
        {
            Id = post.Id,
            Title = post.Title,
            Slug = post.Slug,
            Created = created
        };
    }
}
