namespace TravelBlog.Web.Services;

public sealed record StoredImage(string PublicUrl, string ObjectKey);

public interface IImageStorage
{
    Task<StoredImage> UploadAsync(
        Stream content,
        string contentType,
        string fileExtension,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        string objectKey,
        CancellationToken cancellationToken);
}
