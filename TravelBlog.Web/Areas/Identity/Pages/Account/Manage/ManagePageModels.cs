using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using TravelBlog.Web.Authorization;
using TravelBlog.Web.Models;
using TravelBlog.Web.Services;

namespace TravelBlog.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class IndexModel(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IAccountEmailService accountEmailService) : PageModel
{
    [BindProperty] public ProfileInput Input { get; set; } = new();
    [TempData] public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return NotFound();
        Input = new ProfileInput { DisplayName = user.DisplayName, Email = user.Email ?? string.Empty };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return NotFound();
        if (!ModelState.IsValid) return Page();

        user.DisplayName = Input.DisplayName.Trim();
        var emailChanged = !string.Equals(
            user.Email,
            Input.Email,
            StringComparison.OrdinalIgnoreCase);
        if (emailChanged)
        {
            user.EmailConfirmed = false;
            user.LastConfirmationEmailSentAt = null;
            var emailResult = await userManager.SetEmailAsync(user, Input.Email.Trim());
            var nameResult = emailResult.Succeeded
                ? await userManager.SetUserNameAsync(user, Input.Email.Trim())
                : emailResult;
            if (!nameResult.Succeeded)
            {
                foreach (var error in nameResult.Errors) ModelState.AddModelError(string.Empty, error.Description);
                return Page();
            }
        }

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }
        await signInManager.RefreshSignInAsync(user);
        if (emailChanged)
        {
            var token = await userManager
                .GenerateEmailConfirmationTokenAsync(user);
            var code = WebEncoders.Base64UrlEncode(
                Encoding.UTF8.GetBytes(token));
            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new
                {
                    area = "Identity",
                    userId = user.Id,
                    code
                },
                protocol: Request.Scheme)!;
            await accountEmailService.SendConfirmationAsync(
                user,
                callbackUrl,
                HttpContext.RequestAborted);
            StatusMessage =
                "Your profile was updated. Confirm your new email to write content.";
        }
        else
        {
            StatusMessage = "Your profile has been updated.";
        }
        return RedirectToPage();
    }

    public sealed class ProfileInput
    {
        [Required, StringLength(100), Display(Name = "Display name")]
        public string DisplayName { get; set; } = string.Empty;
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    }
}

[Authorize]
[EnableRateLimiting(RateLimitPolicyNames.Email)]
public class EmailModel(
    UserManager<ApplicationUser> userManager,
    IAccountEmailService accountEmailService) : PageModel
{
    public string Email { get; private set; } = string.Empty;
    public bool IsVerified { get; private set; }
    [TempData] public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync() =>
        await LoadAsync() ? Page() : NotFound();

    public async Task<IActionResult> OnPostSendVerificationAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return NotFound();
        }

        if (!user.EmailConfirmed)
        {
            var token = await userManager
                .GenerateEmailConfirmationTokenAsync(user);
            var code = WebEncoders.Base64UrlEncode(
                Encoding.UTF8.GetBytes(token));
            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new
                {
                    area = "Identity",
                    userId = user.Id,
                    code
                },
                protocol: Request.Scheme)!;
            await accountEmailService.SendConfirmationAsync(
                user,
                callbackUrl,
                HttpContext.RequestAborted);
        }

        StatusMessage =
            "If your email needs verification, a confirmation message has been sent.";
        return RedirectToPage();
    }

    private async Task<bool> LoadAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return false;
        }

        Email = user.Email ?? string.Empty;
        IsVerified = user.EmailConfirmed;
        return true;
    }
}

[Authorize]
public class ChangePasswordModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager) : PageModel
{
    [BindProperty] public PasswordInput Input { get; set; } = new();
    [TempData] public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var user = await userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        var result = await userManager.ChangePasswordAsync(user, Input.OldPassword, Input.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }
        await signInManager.RefreshSignInAsync(user);
        StatusMessage = "Your password has been changed.";
        return RedirectToPage();
    }

    public sealed class PasswordInput
    {
        [Required, DataType(DataType.Password), Display(Name = "Current password")]
        public string OldPassword { get; set; } = string.Empty;
        [Required, DataType(DataType.Password), Display(Name = "New password")]
        public string NewPassword { get; set; } = string.Empty;
        [DataType(DataType.Password), Display(Name = "Confirm new password"), Compare(nameof(NewPassword))]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
