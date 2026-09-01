using System.Text;
using System.Text.RegularExpressions;

namespace TravelBlog.Web.Services;

public static partial class ContactPostSlug
{
    public static string Create(string title)
    {
        var normalized = Slugify(title);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "message";
        }

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var slug = $"{normalized}-{suffix}";
        return slug.Length <= 160
            ? slug
            : slug[..160].TrimEnd('-');
    }

    private static string Slugify(string value)
    {
        var lowered = value.Trim().ToLowerInvariant();
        var builder = new StringBuilder(lowered.Length);

        foreach (var character in lowered)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(character);
            }
            else if (character is ' ' or '_' or '-')
            {
                builder.Append('-');
            }
        }

        return DuplicateHyphenPattern().Replace(builder.ToString(), "-").Trim('-');
    }

    [GeneratedRegex("-{2,}")]
    private static partial Regex DuplicateHyphenPattern();
}
