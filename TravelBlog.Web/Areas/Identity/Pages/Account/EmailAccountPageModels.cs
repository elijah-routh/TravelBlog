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
public class RegisterConfirmationModel : PageModel;

[AllowAnonymous]
public class ConfirmEmailModel(
    UserManager<ApplicationUser> userManager) : PageModel
{
    public bool Succeeded { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        string? userId,
        string? code)
    {
        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(code))
        {
            return BadRequest();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFound();
        }

        try
        {
            var decodedCode = Encoding.UTF8.GetString(
                WebEncoders.Base64UrlDecode(code));
            var result = await userManager.ConfirmEmailAsync(
                user,
                decodedCode);
            Succeeded = result.Succeeded;
        }
        catch (FormatException)
        {
            Succeeded = false;
        }

        return Page();
    }
}

[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicyNames.Email)]
public class ForgotPasswordModel(
    UserManager<ApplicationUser> userManager,
    IAccountEmailService accountEmailService) : PageModel
{
    [BindProperty]
    public ForgotPasswordInput Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await userManager.FindByEmailAsync(Input.Email.Trim());
        if (user is not null)
        {
            var token = await userManager
                .GeneratePasswordResetTokenAsync(user);
            var code = WebEncoders.Base64UrlEncode(
                Encoding.UTF8.GetBytes(token));
            var callbackUrl = Url.Page(
                "/Account/ResetPassword",
                pageHandler: null,
                values: new
                {
                    area = "Identity",
                    code,
                    email = user.Email
                },
                protocol: Request.Scheme)!;
            await accountEmailService.SendPasswordResetAsync(
                user,
                callbackUrl,
                HttpContext.RequestAborted);
        }

        return RedirectToPage("./ForgotPasswordConfirmation");
    }

    public sealed class ForgotPasswordInput
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}

[AllowAnonymous]
public class ForgotPasswordConfirmationModel : PageModel;

[AllowAnonymous]
public class ResetPasswordModel(
    UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty]
    public ResetPasswordInput Input { get; set; } = new();

    public IActionResult OnGet(string? code, string? email)
    {
        if (string.IsNullOrWhiteSpace(code) ||
            string.IsNullOrWhiteSpace(email))
        {
            return BadRequest();
        }

        try
        {
            Input.Code = Encoding.UTF8.GetString(
                WebEncoders.Base64UrlDecode(code));
            Input.Email = email;
            return Page();
        }
        catch (FormatException)
        {
            return BadRequest();
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await userManager.FindByEmailAsync(Input.Email.Trim());
        if (user is null)
        {
            return RedirectToPage("./ResetPasswordConfirmation");
        }

        var result = await userManager.ResetPasswordAsync(
            user,
            Input.Code,
            Input.Password);
        if (result.Succeeded)
        {
            return RedirectToPage("./ResetPasswordConfirmation");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return Page();
    }

    public sealed class ResetPasswordInput
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare(nameof(Password))]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required]
        public string Code { get; set; } = string.Empty;
    }
}

[AllowAnonymous]
public class ResetPasswordConfirmationModel : PageModel;

[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicyNames.Email)]
public class ResendEmailConfirmationModel(
    UserManager<ApplicationUser> userManager,
    IAccountEmailService accountEmailService) : PageModel
{
    [BindProperty]
    public ResendEmailConfirmationInput Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await userManager.FindByEmailAsync(Input.Email.Trim());
        if (user is not null &&
            !await userManager.IsEmailConfirmedAsync(user))
        {
            var token = await userManager
                .GenerateEmailConfirmationTokenAsync(user);
            var code = WebEncoders.Base64UrlEncode(
                Encoding.UTF8.GetBytes(token));
            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId = user.Id, code },
                protocol: Request.Scheme)!;
            await accountEmailService.SendConfirmationAsync(
                user,
                callbackUrl,
                HttpContext.RequestAborted);
        }

        return RedirectToPage("./RegisterConfirmation");
    }

    public sealed class ResendEmailConfirmationInput
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
