using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TravelBlog.Web.Data;
using TravelBlog.Web.Models;
using TravelBlog.Web.Services;

namespace TravelBlog.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class PersonalDataModel(
    UserManager<ApplicationUser> userManager) : PageModel
{
    public bool CanDeleteAccount { get; private set; }
    [TempData] public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        CanDeleteAccount = user.Id != BootstrapAdminConstants.UserId;
        return Page();
    }

    public async Task<IActionResult> OnPostDownloadAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        var personalData = new Dictionary<string, string?>
        {
            ["DisplayName"] = user.DisplayName,
            ["Email"] = user.Email,
            ["UserName"] = user.UserName,
            ["EmailConfirmed"] = user.EmailConfirmed.ToString(),
            ["PhoneNumber"] = user.PhoneNumber,
            ["TwoFactorEnabled"] = user.TwoFactorEnabled.ToString(),
            ["AuthenticatorKey"] =
                await userManager.GetAuthenticatorKeyAsync(user)
        };

        var payload = JsonSerializer.SerializeToUtf8Bytes(personalData);
        return File(payload, "application/json", "PersonalData.json");
    }
}

[Authorize]
public class DeletePersonalDataModel(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IAccountAnonymizationService accountAnonymizationService) : PageModel
{
    [BindProperty] public DeleteInput Input { get; set; } = new();
    [TempData] public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return NotFound();
        if (user.Id == BootstrapAdminConstants.UserId)
        {
            return RedirectToPage("./PersonalData");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return NotFound();
        if (user.Id == BootstrapAdminConstants.UserId)
        {
            return RedirectToPage("./PersonalData");
        }

        if (!ModelState.IsValid) return Page();

        if (!await userManager.CheckPasswordAsync(user, Input.Password))
        {
            ModelState.AddModelError(
                "Input.Password",
                "Incorrect password.");
            return Page();
        }

        var result = await accountAnonymizationService.AnonymizeSelfAsync(
            user.Id,
            HttpContext.RequestAborted);
        if (result.Status != AccountAnonymizationStatus.Succeeded)
        {
            ModelState.AddModelError(
                string.Empty,
                "Your account could not be deleted. No changes were saved.");
            return Page();
        }

        await signInManager.SignOutAsync();
        return Redirect("~/");
    }

    public sealed class DeleteInput
    {
        [Required, DataType(DataType.Password), Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;
    }
}
