namespace TravelBlog.Web.Services;

public static class ViewerCookie
{
    public const string Name = "tb_viewer";

    public const int KeyLength = 32;

    public static string GetOrCreate(HttpContext httpContext)
    {
        if (httpContext.Request.Cookies.TryGetValue(Name, out var existing) &&
            IsValidKey(existing))
        {
            return existing;
        }

        var created = Guid.NewGuid().ToString("N");
        httpContext.Response.Cookies.Append(
            Name,
            created,
            new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = httpContext.Request.IsHttps,
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                Path = "/"
            });
        return created;
    }

    public static bool IsValidKey(string? key) =>
        key is { Length: KeyLength } &&
        key.All(character => char.IsAsciiHexDigit(character));
}
