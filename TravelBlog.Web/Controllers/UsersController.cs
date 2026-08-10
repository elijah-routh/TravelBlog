using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelBlog.Web.Authorization;
using TravelBlog.Web.Models;

namespace TravelBlog.Web.Controllers;

[Authorize(Roles = RoleNames.Admin)]
public class UsersController(UserManager<ApplicationUser> userManager) : Controller
{
    public async Task<IActionResult> Index()
    {
        var users = await userManager.Users
            .OrderBy(user => user.DisplayName)
            .ToListAsync();
        var adminIds = (await userManager.GetUsersInRoleAsync(RoleNames.Admin))
            .Select(user => user.Id)
            .ToHashSet();

        return View(users.Select(user => new UserAdministrationViewModel
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email ?? string.Empty,
            IsAdmin = adminIds.Contains(user.Id)
        }).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Promote(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        if (!await userManager.IsInRoleAsync(user, RoleNames.Admin))
        {
            var result = await userManager.AddToRoleAsync(user, RoleNames.Admin);
            if (!result.Succeeded) TempData["ErrorMessage"] = JoinErrors(result);
            else TempData["StatusMessage"] = $"{user.DisplayName} is now an administrator.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Demote(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        if (await userManager.IsInRoleAsync(user, RoleNames.Admin))
        {
            var admins = await userManager.GetUsersInRoleAsync(RoleNames.Admin);
            if (admins.Count <= 1)
            {
                TempData["ErrorMessage"] = "The final administrator cannot be demoted.";
                return RedirectToAction(nameof(Index));
            }

            var result = await userManager.RemoveFromRoleAsync(user, RoleNames.Admin);
            if (!result.Succeeded) TempData["ErrorMessage"] = JoinErrors(result);
            else TempData["StatusMessage"] = $"{user.DisplayName} is no longer an administrator.";
        }
        return RedirectToAction(nameof(Index));
    }

    private static string JoinErrors(IdentityResult result) =>
        string.Join(" ", result.Errors.Select(error => error.Description));
}
