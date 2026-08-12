using Microsoft.AspNetCore.Http;

namespace TravelBlog.Web.Services;

public sealed record ImageValidationResult(
    bool IsValid,
    string? ContentType = null,
    string? FileExtension = null,
    string? ErrorMessage = null);

public static class ImageUploadValidator
{
    public const long MaximumFileSize = 5 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<
        string,
        (string Extension, Func<ReadOnlyMemory<byte>, bool> Matches)> Formats =
        new Dictionary<
            string,
            (string, Func<ReadOnlyMemory<byte>, bool>)>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ("jpg", bytes =>
                StartsWith(bytes.Span, [0xFF, 0xD8, 0xFF])),
            ["image/png"] = ("png", bytes =>
                StartsWith(
                    bytes.Span,
                    [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])),
            ["image/gif"] = ("gif", bytes =>
                StartsWith(bytes.Span, "GIF87a"u8) ||
                StartsWith(bytes.Span, "GIF89a"u8)),
            ["image/webp"] = ("webp", bytes =>
                bytes.Length >= 12 &&
                bytes.Span[..4].SequenceEqual("RIFF"u8) &&
                bytes.Span.Slice(8, 4).SequenceEqual("WEBP"u8))
        };

    public static async Task<ImageValidationResult> ValidateAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
        {
            return Invalid("The featured image is empty.");
        }

        if (file.Length > MaximumFileSize)
        {
            return Invalid("The featured image cannot exceed 5 MB.");
        }

        var declaredType = file.ContentType
            .Split(';', 2)[0]
            .Trim()
            .ToLowerInvariant();

        if (!Formats.TryGetValue(declaredType, out var format))
        {
            return Invalid(
                "Choose a JPEG, PNG, WebP, or GIF image.");
        }

        var signature = new byte[12];
        var bytesRead = 0;

        await using (var stream = file.OpenReadStream())
        {
            while (bytesRead < signature.Length)
            {
                var read = await stream.ReadAsync(
                    signature.AsMemory(bytesRead),
                    cancellationToken);

                if (read == 0)
                {
                    break;
                }

                bytesRead += read;
            }
        }

        if (!format.Matches(signature.AsMemory(0, bytesRead)))
        {
            return Invalid(
                "The file contents do not match its declared image type.");
        }

        return new ImageValidationResult(
            true,
            declaredType,
            format.Extension);
    }

    private static ImageValidationResult Invalid(string message)
    {
        return new ImageValidationResult(false, ErrorMessage: message);
    }

    private static bool StartsWith(
        ReadOnlySpan<byte> bytes,
        ReadOnlySpan<byte> prefix)
    {
        return bytes.StartsWith(prefix);
    }
}
