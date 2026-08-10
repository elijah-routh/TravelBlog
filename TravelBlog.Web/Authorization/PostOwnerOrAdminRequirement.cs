using Microsoft.AspNetCore.Authorization;

namespace TravelBlog.Web.Authorization;

public sealed class PostOwnerOrAdminRequirement : IAuthorizationRequirement;
