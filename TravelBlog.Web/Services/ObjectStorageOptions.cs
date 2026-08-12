using System.ComponentModel.DataAnnotations;

namespace TravelBlog.Web.Services;

public sealed class ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";

    [Required]
    public string Endpoint { get; set; } = string.Empty;

    [Required]
    public string Region { get; set; } = string.Empty;

    public bool ForcePathStyle { get; set; }

    [Required]
    public string Bucket { get; set; } = string.Empty;

    [Required]
    public string AccessKey { get; set; } = string.Empty;

    [Required]
    public string SecretKey { get; set; } = string.Empty;

    [Required]
    public string PublicBaseUrl { get; set; } = string.Empty;

    public string KeyPrefix { get; set; } = "featured-images";
}
