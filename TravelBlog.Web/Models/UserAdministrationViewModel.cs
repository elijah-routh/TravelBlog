namespace TravelBlog.Web.Models;

public sealed class UserAdministrationViewModel
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Email { get; init; }
    public bool IsAdmin { get; init; }
    public bool IsVerified { get; init; }
    public bool IsBootstrapAdmin { get; init; }
    public bool CanRemove { get; init; }
    public bool IsLockedOut { get; init; }
    public bool CanUnlock { get; init; }
    public bool IsBlocked { get; init; }
    public bool CanBlock { get; init; }
}

public sealed class RemoveUserViewModel
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
}
