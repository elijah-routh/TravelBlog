namespace TravelBlog.Web.Models;

public sealed class UserAdministrationViewModel
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Email { get; init; }
    public bool IsAdmin { get; init; }
}
