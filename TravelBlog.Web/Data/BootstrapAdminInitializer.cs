using Microsoft.AspNetCore.Identity;
using TravelBlog.Web.Authorization;
using TravelBlog.Web.Models;

namespace TravelBlog.Web.Data;

public static class BootstrapAdminInitializer
{
    public static async Task InitializeAsync(
        IServiceProvider services,
        IConfiguration configuration)
    {
        var userManager =
            services.GetRequiredService<UserManager<ApplicationUser>>();

        var admin = await userManager.FindByIdAsync(
            BootstrapAdminConstants.UserId);

        if (admin is not null &&
            await userManager.HasPasswordAsync(admin) &&
            await userManager.IsInRoleAsync(admin, RoleNames.Admin))
        {
            return;
        }

        var email = RequireSetting(configuration, "BootstrapAdmin:Email");
        var password = RequireSetting(
            configuration,
            "BootstrapAdmin:Password");
        var displayName = RequireSetting(
            configuration,
            "BootstrapAdmin:DisplayName");

        var roleManager =
            services.GetRequiredService<RoleManager<IdentityRole>>();

        var deletedUser = await userManager.FindByIdAsync(
            DeletedUserConstants.UserId);
        if (deletedUser is null ||
            deletedUser.DisplayName != DeletedUserConstants.DisplayName ||
            deletedUser.Email is not null ||
            deletedUser.UserName is not null ||
            await userManager.HasPasswordAsync(deletedUser) ||
            (await userManager.GetRolesAsync(deletedUser)).Count != 0)
        {
            throw new InvalidOperationException(
                "The deleted-user sentinel is missing or invalid. " +
                "Apply the latest database migration before startup.");
        }

        if (!await roleManager.RoleExistsAsync(RoleNames.Admin))
        {
            EnsureSucceeded(
                await roleManager.CreateAsync(
                    new IdentityRole(RoleNames.Admin)),
                "create the Admin role");
        }

        if (admin is null)
        {
            throw new InvalidOperationException(
                "The bootstrap administrator placeholder is missing. " +
                "Apply the latest database migration before startup.");
        }

        var conflictingUser = await userManager.FindByEmailAsync(email);

        if (conflictingUser is not null &&
            conflictingUser.Id != admin.Id)
        {
            throw new InvalidOperationException(
                "BootstrapAdmin:Email belongs to another account.");
        }

        admin.Email = email;
        admin.UserName = email;
        admin.DisplayName = displayName;
        admin.EmailConfirmed = true;

        EnsureSucceeded(
            await userManager.UpdateAsync(admin),
            "configure the bootstrap administrator");

        if (!await userManager.HasPasswordAsync(admin))
        {
            EnsureSucceeded(
                await userManager.AddPasswordAsync(admin, password),
                "set the bootstrap administrator password");
        }

        if (!await userManager.IsInRoleAsync(admin, RoleNames.Admin))
        {
            EnsureSucceeded(
                await userManager.AddToRoleAsync(admin, RoleNames.Admin),
                "assign the bootstrap administrator role");
        }
    }

    private static string RequireSetting(
        IConfiguration configuration,
        string key)
    {
        var value = configuration[key];

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Required configuration '{key}' is missing.");
        }

        return value;
    }

    private static void EnsureSucceeded(
        IdentityResult result,
        string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join(
            "; ",
            result.Errors.Select(error => error.Description));

        throw new InvalidOperationException(
            $"Unable to {operation}: {errors}");
    }
}
