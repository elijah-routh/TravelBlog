using Microsoft.AspNetCore.Mvc;

namespace TravelBlog.Web.Services;

public interface INutPlaceholderImages
{
    string GetImageUrl(int postId, IUrlHelper urlHelper);
}
