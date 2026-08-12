using TravelBlog.Web.Services;

namespace TravelBlog.Web.Tests;

public sealed record FakeImageUpload(
    string PublicUrl,
    string ObjectKey,
    string ContentType,
    string FileExtension,
    byte[] Content);

public sealed class FakeImageStorage : IImageStorage
{
    private readonly object _sync = new();
    private readonly List<FakeImageUpload> _uploads = [];
    private readonly List<string> _deletedObjectKeys = [];
    private int _nextImageNumber = 1;

    public IReadOnlyList<FakeImageUpload> Uploads
    {
        get
        {
            lock (_sync)
            {
                return _uploads.ToArray();
            }
        }
    }

    public IReadOnlyList<string> DeletedObjectKeys
    {
        get
        {
            lock (_sync)
            {
                return _deletedObjectKeys.ToArray();
            }
        }
    }

    public async Task<StoredImage> UploadAsync(
        Stream content,
        string contentType,
        string fileExtension,
        CancellationToken cancellationToken)
    {
        await using var copy = new MemoryStream();
        await content.CopyToAsync(copy, cancellationToken);

        lock (_sync)
        {
            var extension = fileExtension.TrimStart('.').ToLowerInvariant();
            var objectKey =
                $"testing/image-{_nextImageNumber++:D4}.{extension}";
            var publicUrl =
                $"https://images.example.test/{objectKey}";

            _uploads.Add(new FakeImageUpload(
                publicUrl,
                objectKey,
                contentType,
                extension,
                copy.ToArray()));

            return new StoredImage(publicUrl, objectKey);
        }
    }

    public Task DeleteAsync(
        string objectKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            _deletedObjectKeys.Add(objectKey);
        }

        return Task.CompletedTask;
    }

    public void Reset()
    {
        lock (_sync)
        {
            _uploads.Clear();
            _deletedObjectKeys.Clear();
            _nextImageNumber = 1;
        }
    }
}
