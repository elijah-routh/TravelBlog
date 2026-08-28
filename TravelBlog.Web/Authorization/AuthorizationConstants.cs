namespace TravelBlog.Web.Authorization;

public static class RoleNames
{
    public const string Admin = "Admin";
}

public static class PolicyNames
{
    public const string PostOwnerOrAdmin = "PostOwnerOrAdmin";
    public const string VerifiedEmail = "VerifiedEmail";
    public const string BootstrapAdmin = "BootstrapAdmin";
}

public static class RateLimitPolicyNames
{
    public const string Registration = "Registration";
    public const string Email = "Email";
    public const string Login = "Login";
    public const string Comments = "Comments";
}
