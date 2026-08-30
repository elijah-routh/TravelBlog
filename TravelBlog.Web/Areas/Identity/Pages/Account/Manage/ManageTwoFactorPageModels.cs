using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TravelBlog.Web.Models;

namespace TravelBlog.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class TwoFactorAuthenticationModel(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : PageModel
{
    public bool HasAuthenticator { get; private set; }
    public bool Is2faEnabled { get; private set; }
    public bool IsMachineRemembered { get; private set; }
    public int RecoveryCodesLeft { get; private set; }
    [TempData] public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync() =>
        await LoadAsync() ? Page() : NotFound();

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        await signInManager.ForgetTwoFactorClientAsync();
        StatusMessage =
            "This browser will ask for a two-factor code the next time you sign in.";
        return RedirectToPage();
    }

    private async Task<bool> LoadAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return false;

        HasAuthenticator = await userManager.GetAuthenticatorKeyAsync(user) is not null;
        Is2faEnabled = await userManager.GetTwoFactorEnabledAsync(user);
        IsMachineRemembered = await signInManager.IsTwoFactorClientRememberedAsync(user);
        RecoveryCodesLeft = await userManager.CountRecoveryCodesAsync(user);
        return true;
    }
}

[Authorize]
public class EnableAuthenticatorModel(
    UserManager<ApplicationUser> userManager) : PageModel
{
    public string SharedKey { get; private set; } = string.Empty;
    [BindProperty] public AuthenticatorInput Input { get; set; } = new();
    [TempData] public string? StatusMessage { get; set; }
    [TempData] public string[]? RecoveryCodes { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return NotFound();
        await LoadSharedKeyAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return NotFound();
        if (!ModelState.IsValid)
        {
            await LoadSharedKeyAsync(user);
            return Page();
        }

        var verificationCode = Input.Code
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
        var isValid = await userManager.VerifyTwoFactorTokenAsync(
            user,
            userManager.Options.Tokens.AuthenticatorTokenProvider,
            verificationCode);
        if (!isValid)
        {
            ModelState.AddModelError(
                "Input.Code",
                "Verification code is invalid.");
            await LoadSharedKeyAsync(user);
            return Page();
        }

        await userManager.SetTwoFactorEnabledAsync(user, true);
        StatusMessage = "Your authenticator app has been verified.";
        if (await userManager.CountRecoveryCodesAsync(user) == 0)
        {
            var recoveryCodes = await userManager
                .GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
            RecoveryCodes = recoveryCodes!.ToArray();
            return RedirectToPage("./ShowRecoveryCodes");
        }

        return RedirectToPage("./TwoFactorAuthentication");
    }

    private async Task LoadSharedKeyAsync(ApplicationUser user)
    {
        var unformattedKey = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(unformattedKey))
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            unformattedKey = await userManager.GetAuthenticatorKeyAsync(user);
        }

        SharedKey = FormatKey(unformattedKey!);
    }

    private static string FormatKey(string unformattedKey)
    {
        var result = new StringBuilder();
        var currentPosition = 0;
        while (currentPosition + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
            currentPosition += 4;
        }

        if (currentPosition < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition));
        }

        return result.ToString().ToLowerInvariant();
    }

    public sealed class AuthenticatorInput
    {
        [Required]
        [StringLength(7, MinimumLength = 6)]
        [DataType(DataType.Text)]
        [Display(Name = "Verification code")]
        public string Code { get; set; } = string.Empty;
    }
}

[Authorize]
public class Disable2faModel(UserManager<ApplicationUser> userManager) : PageModel
{
    [TempData] public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return NotFound();
        if (!await userManager.GetTwoFactorEnabledAsync(user))
        {
            return RedirectToPage("./TwoFactorAuthentication");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        var result = await userManager.SetTwoFactorEnabledAsync(user, false);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        StatusMessage = "Two-factor authentication has been disabled.";
        return RedirectToPage("./TwoFactorAuthentication");
    }
}

[Authorize]
public class ResetAuthenticatorModel(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : PageModel
{
    [TempData] public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User);
        return user is null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        await userManager.SetTwoFactorEnabledAsync(user, false);
        await userManager.ResetAuthenticatorKeyAsync(user);
        await signInManager.RefreshSignInAsync(user);
        StatusMessage =
            "Your authenticator app key was reset. Set up the app again to turn 2FA back on.";
        return RedirectToPage("./EnableAuthenticator");
    }
}

[Authorize]
public class GenerateRecoveryCodesModel(
    UserManager<ApplicationUser> userManager) : PageModel
{
    [TempData] public string? StatusMessage { get; set; }
    [TempData] public string[]? RecoveryCodes { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return NotFound();
        if (!await userManager.GetTwoFactorEnabledAsync(user))
        {
            return RedirectToPage("./TwoFactorAuthentication");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return NotFound();
        if (!await userManager.GetTwoFactorEnabledAsync(user))
        {
            return RedirectToPage("./TwoFactorAuthentication");
        }

        var recoveryCodes = await userManager
            .GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        RecoveryCodes = recoveryCodes!.ToArray();
        StatusMessage = "You have generated new recovery codes.";
        return RedirectToPage("./ShowRecoveryCodes");
    }
}

[Authorize]
public class ShowRecoveryCodesModel : PageModel
{
    [TempData] public string? StatusMessage { get; set; }
    [TempData] public string[]? RecoveryCodes { get; set; }

    public IActionResult OnGet()
    {
        if (RecoveryCodes is null || RecoveryCodes.Length == 0)
        {
            return RedirectToPage("./TwoFactorAuthentication");
        }

        return Page();
    }
}
