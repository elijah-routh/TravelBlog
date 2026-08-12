using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TravelBlog.Web.Data;
using TravelBlog.Web.Models;
using TravelBlog.Web.Services;

namespace TravelBlog.Web.Tests;

[Collection(IntegrationTestCollection.Name)]
public sealed class FeaturedImageUploadTests
{
    private const string Password = "Test-pass1!";
    private static readonly byte[] PngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01];

    private readonly TravelBlogWebApplicationFactory _factory;

    public FeaturedImageUploadTests(
        TravelBlogWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ImageStorage.Reset();
    }

    [Fact]
    public async Task AuthenticatedCreateUploadsPngAndPersistsStorageValues()
    {
        var author = await CreateUserAsync("Image Author");
        var other = await CreateUserAsync("Wrong Author");
        using var client = await LoginAsync(author.Email!);
        var token = await GetAntiforgeryTokenAsync(client, "/Posts/Create");
        var slug = UniqueSlug("image-create");
        using var form = MultipartForm(
            token,
            [
                ("Title", "Uploaded image"),
                ("Slug", slug),
                ("Content", "Image upload integration test."),
                ("Category", "1"),
                ("IsPublished", "true"),
                ("AuthorId", other.Id)
            ],
            PngBytes,
            "image/png",
            "featured.png");

        var response = await client.PostAsync("/Posts/Create", form);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var upload = Assert.Single(_factory.ImageStorage.Uploads);
        Assert.Equal("image/png", upload.ContentType);
        Assert.Equal("png", upload.FileExtension);
        Assert.Equal(PngBytes, upload.Content);
        await WithServicesAsync(async services =>
        {
            var post = await services
                .GetRequiredService<BlogDbContext>()
                .Posts.SingleAsync(candidate => candidate.Slug == slug);
            Assert.Equal(author.Id, post.AuthorId);
            Assert.NotEqual(other.Id, post.AuthorId);
            Assert.Equal(upload.PublicUrl, post.ImagePath);
            Assert.Equal(upload.ObjectKey, post.ImageObjectKey);
        });
    }

    [Fact]
    public async Task ReplacingImageDeletesPreviousManagedObject()
    {
        var owner = await CreateUserAsync("Replacement Owner");
        var post = await CreatePostAsync(
            owner,
            "replace",
            "https://images.example.test/managed/old.png",
            "managed/old.png");
        using var client = await LoginAsync(owner.Email!);
        var token = await GetAntiforgeryTokenAsync(
            client,
            $"/Posts/Edit/{post.Id}");
        using var form = EditMultipartForm(
            token,
            post,
            PngBytes,
            "image/png",
            "replacement.png");

        var response = await client.PostAsync(
            $"/Posts/Edit/{post.Id}",
            form);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var upload = Assert.Single(_factory.ImageStorage.Uploads);
        Assert.Equal(
            ["managed/old.png"],
            _factory.ImageStorage.DeletedObjectKeys);
        await WithServicesAsync(async services =>
        {
            var stored = await services
                .GetRequiredService<BlogDbContext>()
                .Posts.SingleAsync(candidate => candidate.Id == post.Id);
            Assert.Equal(upload.PublicUrl, stored.ImagePath);
            Assert.Equal(upload.ObjectKey, stored.ImageObjectKey);
        });
    }

    [Fact]
    public async Task RemovingImageClearsValuesAndDeletesManagedObject()
    {
        var owner = await CreateUserAsync("Removal Owner");
        var post = await CreatePostAsync(
            owner,
            "remove",
            "https://images.example.test/managed/remove.png",
            "managed/remove.png");
        using var client = await LoginAsync(owner.Email!);
        var token = await GetAntiforgeryTokenAsync(
            client,
            $"/Posts/Edit/{post.Id}");
        using var form = EditMultipartForm(
            token,
            post,
            additionalFields: [("RemoveFeaturedImage", "true")]);

        var response = await client.PostAsync(
            $"/Posts/Edit/{post.Id}",
            form);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Empty(_factory.ImageStorage.Uploads);
        Assert.Equal(
            ["managed/remove.png"],
            _factory.ImageStorage.DeletedObjectKeys);
        await WithServicesAsync(async services =>
        {
            var stored = await services
                .GetRequiredService<BlogDbContext>()
                .Posts.SingleAsync(candidate => candidate.Id == post.Id);
            Assert.Null(stored.ImagePath);
            Assert.Null(stored.ImageObjectKey);
        });
    }

    [Fact]
    public async Task DeletingPostDeletesManagedObject()
    {
        var owner = await CreateUserAsync("Delete Owner");
        var post = await CreatePostAsync(
            owner,
            "delete",
            "https://images.example.test/managed/delete.png",
            "managed/delete.png");
        using var client = await LoginAsync(owner.Email!);
        var token = await GetAntiforgeryTokenAsync(
            client,
            $"/Posts/Delete/{post.Id}");
        using var form = MultipartForm(
            token,
            [("id", post.Id.ToString())]);

        var response = await client.PostAsync(
            $"/Posts/Delete/{post.Id}",
            form);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            ["managed/delete.png"],
            _factory.ImageStorage.DeletedObjectKeys);
        await WithServicesAsync(async services =>
        {
            Assert.False(await services
                .GetRequiredService<BlogDbContext>()
                .Posts.AnyAsync(candidate => candidate.Id == post.Id));
        });
    }

    [Theory]
    [InlineData("text/plain", true)]
    [InlineData("image/png", false)]
    public async Task InvalidTypeOrSignatureDoesNotUpload(
        string contentType,
        bool usePngSignature)
    {
        var author = await CreateUserAsync("Invalid Image Author");
        using var client = await LoginAsync(author.Email!);
        var token = await GetAntiforgeryTokenAsync(client, "/Posts/Create");
        var slug = UniqueSlug("invalid-image");
        using var form = MultipartForm(
            token,
            ValidCreateFields(slug),
            usePngSignature ? PngBytes : "not a png"u8.ToArray(),
            contentType,
            "invalid.png");

        var response = await client.PostAsync("/Posts/Create", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(_factory.ImageStorage.Uploads);
        await AssertPostDoesNotExistAsync(slug);
    }

    [Fact]
    public async Task ImageLargerThanFiveMegabytesDoesNotUpload()
    {
        var author = await CreateUserAsync("Large Image Author");
        using var client = await LoginAsync(author.Email!);
        var token = await GetAntiforgeryTokenAsync(client, "/Posts/Create");
        var slug = UniqueSlug("large-image");
        var content = new byte[ImageUploadValidator.MaximumFileSize + 1];
        PngBytes.CopyTo(content, 0);
        using var form = MultipartForm(
            token,
            ValidCreateFields(slug),
            content,
            "image/png",
            "large.png");

        var response = await client.PostAsync("/Posts/Create", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(_factory.ImageStorage.Uploads);
        await AssertPostDoesNotExistAsync(slug);
    }

    [Fact]
    public async Task EditWithoutFilePreservesLegacyImagePath()
    {
        var owner = await CreateUserAsync("Legacy Image Owner");
        const string legacyPath = "/images/legacy-feature.jpg";
        var post = await CreatePostAsync(
            owner,
            "legacy",
            legacyPath,
            imageObjectKey: null);
        using var client = await LoginAsync(owner.Email!);
        var token = await GetAntiforgeryTokenAsync(
            client,
            $"/Posts/Edit/{post.Id}");
        using var form = EditMultipartForm(token, post);

        var response = await client.PostAsync(
            $"/Posts/Edit/{post.Id}",
            form);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Empty(_factory.ImageStorage.Uploads);
        Assert.Empty(_factory.ImageStorage.DeletedObjectKeys);
        await WithServicesAsync(async services =>
        {
            var stored = await services
                .GetRequiredService<BlogDbContext>()
                .Posts.SingleAsync(candidate => candidate.Id == post.Id);
            Assert.Equal(legacyPath, stored.ImagePath);
            Assert.Null(stored.ImageObjectKey);
        });
    }

    [Fact]
    public async Task UnrelatedUserEditIsForbiddenBeforeImageUpload()
    {
        var owner = await CreateUserAsync("Protected Image Owner");
        var intruder = await CreateUserAsync("Image Intruder");
        var post = await CreatePostAsync(owner, "forbidden-image");
        using var client = await LoginAsync(intruder.Email!);
        var token = await GetAntiforgeryTokenAsync(client, "/Posts/Create");
        using var form = EditMultipartForm(
            token,
            post,
            PngBytes,
            "image/png",
            "intruder.png");

        var response = await client.PostAsync(
            $"/Posts/Edit/{post.Id}",
            form);

        AssertAccessDenied(response);
        Assert.Empty(_factory.ImageStorage.Uploads);
        Assert.Empty(_factory.ImageStorage.DeletedObjectKeys);
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });

    private async Task<HttpClient> LoginAsync(string email)
    {
        var client = CreateClient();
        var token = await GetAntiforgeryTokenAsync(
            client,
            "/Identity/Account/Login");
        using var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Input.Email"] = email,
                ["Input.Password"] = Password,
                ["Input.RememberMe"] = "false",
                ["__RequestVerificationToken"] = token
            });
        var response = await client.PostAsync(
            "/Identity/Account/Login",
            form);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private async Task<ApplicationUser> CreateUserAsync(string displayName)
    {
        ApplicationUser? created = null;
        await WithServicesAsync(async services =>
        {
            var userManager =
                services.GetRequiredService<UserManager<ApplicationUser>>();
            created = new ApplicationUser
            {
                DisplayName = displayName,
                UserName = $"{Guid.NewGuid():N}@example.test",
                EmailConfirmed = true
            };
            created.Email = created.UserName;
            Assert.True(
                (await userManager.CreateAsync(created, Password)).Succeeded);
        });
        return created!;
    }

    private async Task<Post> CreatePostAsync(
        ApplicationUser author,
        string label,
        string? imagePath = null,
        string? imageObjectKey = null)
    {
        Post? created = null;
        await WithServicesAsync(async services =>
        {
            created = new Post
            {
                AuthorId = author.Id,
                Title = $"{label}-{Guid.NewGuid():N}",
                Slug = UniqueSlug(label),
                Content = "Featured image integration test.",
                ImagePath = imagePath,
                ImageObjectKey = imageObjectKey,
                Category = PostCategory.ParodyEditorial,
                IsPublished = true,
                CreatedAt = DateTime.UtcNow
            };
            var context = services.GetRequiredService<BlogDbContext>();
            context.Posts.Add(created);
            await context.SaveChangesAsync();
        });
        return created!;
    }

    private async Task WithServicesAsync(
        Func<IServiceProvider, Task> action)
    {
        using var scope = _factory.Services.CreateScope();
        await action(scope.ServiceProvider);
    }

    private async Task AssertPostDoesNotExistAsync(string slug)
    {
        await WithServicesAsync(async services =>
        {
            Assert.False(await services
                .GetRequiredService<BlogDbContext>()
                .Posts.AnyAsync(candidate => candidate.Slug == slug));
        });
    }

    private static async Task<string> GetAntiforgeryTokenAsync(
        HttpClient client,
        string path)
    {
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, $"No antiforgery token found at {path}.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static MultipartFormDataContent EditMultipartForm(
        string token,
        Post post,
        byte[]? fileContent = null,
        string? contentType = null,
        string? fileName = null,
        (string Key, string Value)[]? additionalFields = null)
    {
        var fields = new List<(string Key, string Value)>
        {
            ("Id", post.Id.ToString()),
            ("Title", $"{post.Title}-edited"),
            ("Slug", post.Slug),
            ("Content", $"{post.Content} Edited."),
            ("Category", "1"),
            ("IsPublished", "true")
        };
        if (additionalFields is not null)
        {
            fields.AddRange(additionalFields);
        }

        return MultipartForm(
            token,
            fields,
            fileContent,
            contentType,
            fileName);
    }

    private static MultipartFormDataContent MultipartForm(
        string token,
        IEnumerable<(string Key, string Value)> fields,
        byte[]? fileContent = null,
        string? contentType = null,
        string? fileName = null)
    {
        var form = new MultipartFormDataContent();
        foreach (var (key, value) in fields.Append(
                     ("__RequestVerificationToken", token)))
        {
            form.Add(new StringContent(value), key);
        }

        if (fileContent is not null)
        {
            var file = new ByteArrayContent(fileContent);
            file.Headers.ContentType = new MediaTypeHeaderValue(contentType!);
            form.Add(file, "FeaturedImage", fileName!);
        }

        return form;
    }

    private static (string Key, string Value)[] ValidCreateFields(
        string slug) =>
        [
            ("Title", "Image validation"),
            ("Slug", slug),
            ("Content", "Image validation integration test."),
            ("Category", "1"),
            ("IsPublished", "true")
        ];

    private static void AssertAccessDenied(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return;
        }

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "/Identity/Account/AccessDenied",
            response.Headers.Location?.AbsolutePath);
    }

    private static string UniqueSlug(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}";
}
