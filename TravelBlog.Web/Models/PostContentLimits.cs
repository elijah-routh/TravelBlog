namespace TravelBlog.Web.Models;

public static class PostContentLimits
{
    public const int MaximumLength = 50_000;
    public const string MaximumLengthError =
        "Post content cannot exceed 50,000 characters.";
}
