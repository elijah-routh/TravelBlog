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

namespace TravelBlog.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicyNames.Registration)]
public class RegisterModel(
    UserManager<ApplicationUser> userManager,
    IAccountEmailService accountEmailService) : PageModel
{
    [BindProperty] public RegisterInput Input { get; set; } = new();
    public string? ReturnUrl { get; set; }

    public void OnGet(string? returnUrl = null) => ReturnUrl = returnUrl;

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl = Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : Url.Content("~/");
        if (!ModelState.IsValid) return Page();

        var user = new ApplicationUser
        {
            DisplayName = Input.DisplayName.Trim(),
            UserName = Input.Email.Trim(),
            Email = Input.Email.Trim()
        };
        var result = await userManager.CreateAsync(user, Input.Password);
        if (result.Succeeded)
        {
            var token = await userManager
                .GenerateEmailConfirmationTokenAsync(user);
            var code = WebEncoders.Base64UrlEncode(
                Encoding.UTF8.GetBytes(token));
            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId = user.Id, code, returnUrl },
                protocol: Request.Scheme)!;
            await accountEmailService.SendConfirmationAsync(
                user,
                callbackUrl,
                HttpContext.RequestAborted);

            return RedirectToPage("./RegisterConfirmation");
        }

        foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
        return Page();
    }

    public sealed class RegisterInput
    {
        [Required, StringLength(100), Display(Name = "Display name")]
        public string DisplayName { get; set; } = string.Empty;
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
        [Required, DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
        [DataType(DataType.Password), Display(Name = "Confirm password"), Compare(nameof(Password))]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}

[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicyNames.Login)]
public class LoginModel(
    SignInManager<ApplicationUser> signInManager) : PageModel
{
    [BindProperty] public LoginInput Input { get; set; } = new();
    public string? ReturnUrl { get; set; }

    public void OnGet(string? returnUrl = null) => ReturnUrl = returnUrl;

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl = Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : Url.Content("~/");
        if (!ModelState.IsValid) return Page();

        var result = await signInManager.PasswordSignInAsync(
            Input.Email.Trim(),
            Input.Password,
            Input.RememberMe,
            lockoutOnFailure: true);
        if (result.Succeeded) return LocalRedirect(returnUrl);
        if (result.IsLockedOut)
        {
            ModelState.AddModelError(
                string.Empty,
                "This account is temporarily locked. Try again in 15 minutes.");
            return Page();
        }
        if (result.RequiresTwoFactor)
        {
            return RedirectToPage(
                "./LoginWith2fa",
                new
                {
                    ReturnUrl = returnUrl,
                    RememberMe = Input.RememberMe
                });
        }
        if (result.IsNotAllowed)
        {
            ModelState.AddModelError(
                string.Empty,
                "You may need to confirm your email before logging in. " +
                "You can request a new confirmation email below.");
            return Page();
        }

        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return Page();
    }

    public sealed class LoginInput
    {
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
        [Required, DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
        [Display(Name = "Remember me")] public bool RememberMe { get; set; }
    }
}

[AllowAnonymous]
public class LoginWith2faModel(
    SignInManager<ApplicationUser> signInManager) : PageModel
{
    [BindProperty]
    public TwoFactorInput Input { get; set; } = new();

    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }

    public async Task<IActionResult> OnGetAsync(
        bool rememberMe,
        string? returnUrl = null)
    {
        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null)
        {
            return RedirectToPage("./Login");
        }

        RememberMe = rememberMe;
        ReturnUrl = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        bool rememberMe,
        string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        if (!ModelState.IsValid)
        {
            RememberMe = rememberMe;
            ReturnUrl = returnUrl;
            return Page();
        }

        var code = Input.TwoFactorCode
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty);
        var result = await signInManager.TwoFactorAuthenticatorSignInAsync(
            code,
            rememberMe,
            Input.RememberMachine);

        if (result.Succeeded)
        {
            return LocalRedirect(returnUrl);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(
                string.Empty,
                "This account is temporarily locked.");
        }
        else
        {
            ModelState.AddModelError(
                string.Empty,
                "Invalid authenticator code.");
        }

        RememberMe = rememberMe;
        ReturnUrl = returnUrl;
        return Page();
    }

    public sealed class TwoFactorInput
    {
        [Required]
        [StringLength(7, MinimumLength = 6)]
        [DataType(DataType.Text)]
        [Display(Name = "Authenticator code")]
        public string TwoFactorCode { get; set; } = string.Empty;

        [Display(Name = "Remember this browser")]
        public bool RememberMachine { get; set; }
    }
}

[Authorize]
public class LogoutModel(SignInManager<ApplicationUser> signInManager) : PageModel
{
    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        await signInManager.SignOutAsync();
        return LocalRedirect(returnUrl ?? Url.Content("~/"));
    }
}

[AllowAnonymous]
public class AccessDeniedModel : PageModel;
