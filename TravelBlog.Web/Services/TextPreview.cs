namespace TravelBlog.Web.Services;

public static class TextPreview
{
    public static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || maxLength < 1)
        {
            return value ?? string.Empty;
        }

        if (value.Length <= maxLength)
        {
            return value;
        }

        return string.Concat(value.AsSpan(0, maxLength - 1), "…");
    }
}
