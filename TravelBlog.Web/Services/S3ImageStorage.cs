using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace TravelBlog.Web.Services;

public sealed class S3ImageStorage : IImageStorage
{
    private readonly IAmazonS3 _s3Client;
    private readonly ObjectStorageOptions _options;

    public S3ImageStorage(
        IAmazonS3 s3Client,
        IOptions<ObjectStorageOptions> options)
    {
        _s3Client = s3Client;
        _options = options.Value;
    }

    public async Task<StoredImage> UploadAsync(
        Stream content,
        string contentType,
        string fileExtension,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        var normalizedExtension = fileExtension.TrimStart('.').ToLowerInvariant();
        var fileName = $"{Guid.NewGuid():N}.{normalizedExtension}";
        var prefix = _options.KeyPrefix.Trim('/');
        var objectKey = string.IsNullOrWhiteSpace(prefix)
            ? fileName
            : $"{prefix}/{fileName}";

        var request = new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = objectKey,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
            UseChunkEncoding = false,
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true
        };

        await _s3Client.PutObjectAsync(request, cancellationToken);

        var publicUrl =
            $"{_options.PublicBaseUrl.TrimEnd('/')}/{EscapeKey(objectKey)}";

        return new StoredImage(publicUrl, objectKey);
    }

    public Task DeleteAsync(
        string objectKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);

        return _s3Client.DeleteObjectAsync(
            _options.Bucket,
            objectKey,
            cancellationToken);
    }

    private static string EscapeKey(string objectKey)
    {
        return string.Join(
            '/',
            objectKey.Split('/').Select(Uri.EscapeDataString));
    }
}
