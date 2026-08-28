using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelBlog.Web.Authorization;
using TravelBlog.Web.Data;
using TravelBlog.Web.Models;
using TravelBlog.Web.Services;

namespace TravelBlog.Web.Controllers;

[Authorize(Roles = RoleNames.Admin)]
public class UsersController(
    UserManager<ApplicationUser> userManager,
    IAccountAnonymizationService accountAnonymizationService,
    TimeProvider timeProvider,
    ILogger<UsersController> logger) : Controller
{
    public async Task<IActionResult> Index()
    {
        var users = await userManager.Users
            .Where(user => user.Id != DeletedUserConstants.UserId)
            .OrderBy(user => user.DisplayName)
            .ToListAsync();
        var adminIds = (await userManager.GetUsersInRoleAsync(RoleNames.Admin))
            .Select(user => user.Id)
            .ToHashSet();

        var isBootstrapViewer =
            BootstrapAdminAccess.IsBootstrapAdmin(User);
        var now = timeProvider.GetUtcNow();
        return View(users.Select(user => new UserAdministrationViewModel
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email ?? string.Empty,
            IsAdmin = adminIds.Contains(user.Id),
            IsVerified = user.EmailConfirmed,
            IsBootstrapAdmin =
                user.Id == BootstrapAdminConstants.UserId,
            CanRemove = isBootstrapViewer &&
                user.Id != BootstrapAdminConstants.UserId,
            IsLockedOut = user.LockoutEnabled &&
                user.LockoutEnd > now,
            CanUnlock = isBootstrapViewer &&
                user.LockoutEnabled &&
                user.LockoutEnd > now
        }).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Promote(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        if (!user.EmailConfirmed)
        {
            TempData["ErrorMessage"] =
                "Verify this user's email before promoting them.";
            return RedirectToAction(nameof(Index));
        }

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

        if (user.Id == BootstrapAdminConstants.UserId)
        {
            TempData["ErrorMessage"] =
                "The bootstrap administrator cannot be demoted.";
            return RedirectToAction(nameof(Index));
        }

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

    [HttpPost]
    [Authorize(Policy = PolicyNames.BootstrapAdmin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(string id)
    {
        if (id == DeletedUserConstants.UserId)
        {
            return Forbid();
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        var unlockResult = await userManager.SetLockoutEndDateAsync(
            user,
            null);
        if (!unlockResult.Succeeded)
        {
            TempData["ErrorMessage"] = JoinErrors(unlockResult);
            return RedirectToAction(nameof(Index));
        }

        var resetResult = await userManager.ResetAccessFailedCountAsync(user);
        if (!resetResult.Succeeded)
        {
            TempData["ErrorMessage"] = JoinErrors(resetResult);
            return RedirectToAction(nameof(Index));
        }

        TempData["StatusMessage"] =
            $"{user.DisplayName}'s account was unlocked.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Policy = PolicyNames.BootstrapAdmin)]
    public async Task<IActionResult> Remove(string id)
    {
        if (IsProtectedRemovalTarget(id))
        {
            return Forbid();
        }

        var user = await userManager.FindByIdAsync(id);
        return user is null
            ? NotFound()
            : View(new RemoveUserViewModel
            {
                Id = user.Id,
                DisplayName = user.DisplayName
            });
    }

    [HttpPost, ActionName("Remove")]
    [Authorize(Policy = PolicyNames.BootstrapAdmin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveConfirmed(
        string id,
        CancellationToken cancellationToken)
    {
        var actorId = userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(actorId))
        {
            return Challenge();
        }

        if (IsProtectedRemovalTarget(id))
        {
            return Forbid();
        }

        try
        {
            var result = await accountAnonymizationService.AnonymizeAsync(
                actorId,
                id,
                cancellationToken);
            switch (result.Status)
            {
                case AccountAnonymizationStatus.Succeeded:
                    TempData["StatusMessage"] =
                        "The account was removed and its content was anonymized.";
                    break;
                case AccountAnonymizationStatus.NotFound:
                    return NotFound();
                case AccountAnonymizationStatus.Forbidden:
                case AccountAnonymizationStatus.ProtectedAccount:
                    return Forbid();
                default:
                    TempData["ErrorMessage"] =
                        "The account could not be removed. No changes were saved.";
                    break;
            }
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Account anonymization failed for actor {ActorId} and " +
                "target {TargetId}.",
                actorId,
                id);
            TempData["ErrorMessage"] =
                "The account could not be removed. No changes were saved.";
        }

        return RedirectToAction(nameof(Index));
    }

    private static string JoinErrors(IdentityResult result) =>
        string.Join(" ", result.Errors.Select(error => error.Description));

    private bool IsProtectedRemovalTarget(string id) =>
        id == userManager.GetUserId(User) ||
        id == BootstrapAdminConstants.UserId ||
        id == DeletedUserConstants.UserId;
}
