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
        var email = RequireSetting(configuration, "BootstrapAdmin:Email");
        var password = RequireSetting(
            configuration,
            "BootstrapAdmin:Password");
        var displayName = RequireSetting(
            configuration,
            "BootstrapAdmin:DisplayName");

        var userManager =
            services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager =
            services.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roleManager.RoleExistsAsync(RoleNames.Admin))
        {
            EnsureSucceeded(
                await roleManager.CreateAsync(
                    new IdentityRole(RoleNames.Admin)),
                "create the Admin role");
        }

        var admin = await userManager.FindByIdAsync(
            BootstrapAdminConstants.UserId);

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
