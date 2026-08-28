using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TravelBlog.Web.Models;
using TravelBlog.Web.Services;

namespace TravelBlog.Web.Tests;

[Collection(IntegrationTestCollection.Name)]
public sealed class EmailIdentityTests
{
    private const string Password = "Test-pass1!";
    private const string NewPassword = "New-test-pass2!";
    private readonly TravelBlogWebApplicationFactory _factory;

    public EmailIdentityTests(TravelBlogWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RegistrationSendsConfirmationAndDoesNotSignIn()
    {
        using var client = CreateClient();
        var email = UniqueEmail("register");
        var response = await RegisterAsync(client, email);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "/Identity/Account/RegisterConfirmation",
            response.Headers.Location?.OriginalString);
        var protectedResponse = await client.GetAsync("/Posts/Create");
        Assert.Equal(HttpStatusCode.Redirect, protectedResponse.StatusCode);
        Assert.Equal(
            "/Identity/Account/Login",
            protectedResponse.Headers.Location?.AbsolutePath);
        Assert.Single(_factory.EmailSender.Messages.Where(
            message => message.Recipient == email));
    }

    [Fact]
    public async Task ConfirmationTokenConfirmsAccountAndAllowsLogin()
    {
        using var client = CreateClient();
        var email = UniqueEmail("confirm");
        await RegisterAsync(client, email);
        var message = Assert.Single(_factory.EmailSender.Messages.Where(
            candidate => candidate.Recipient == email));
        var confirmationUrl = ExtractLink(message.HtmlBody);

        var confirmation = await client.GetAsync(confirmationUrl);

        Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
        await WithUserManagerAsync(async manager =>
        {
            var user = await manager.FindByEmailAsync(email);
            Assert.NotNull(user);
            Assert.True(await manager.IsEmailConfirmedAsync(user));
        });

        var login = await LoginAsync(client, email, Password);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/", login.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task ForgotPasswordResponseDoesNotEnumerateUsers()
    {
        var existingEmail = UniqueEmail("forgot");
        await CreateConfirmedUserAsync(existingEmail);
        using var existingClient = CreateClient();
        using var missingClient = CreateClient();

        var existingResponse = await PostEmailAsync(
            existingClient,
            "/Identity/Account/ForgotPassword",
            existingEmail);
        var missingResponse = await PostEmailAsync(
            missingClient,
            "/Identity/Account/ForgotPassword",
            UniqueEmail("missing"));

        Assert.Equal(HttpStatusCode.Redirect, existingResponse.StatusCode);
        Assert.Equal(
            existingResponse.Headers.Location?.OriginalString,
            missingResponse.Headers.Location?.OriginalString);
        Assert.Equal(
            "/Identity/Account/ForgotPasswordConfirmation",
            existingResponse.Headers.Location?.OriginalString);
        Assert.Single(_factory.EmailSender.Messages.Where(
            message =>
                message.Recipient == existingEmail &&
                message.Subject.Contains("Reset")));
    }

    [Fact]
    public async Task ResetLinkChangesPassword()
    {
        var email = UniqueEmail("reset");
        await CreateConfirmedUserAsync(email);
        using var client = CreateClient();
        await PostEmailAsync(
            client,
            "/Identity/Account/ForgotPassword",
            email);
        var message = Assert.Single(_factory.EmailSender.Messages.Where(
            candidate =>
                candidate.Recipient == email &&
                candidate.Subject.Contains("Reset")));
        var resetUrl = ExtractLink(message.HtmlBody);
        var token = await GetAntiforgeryTokenAsync(client, resetUrl);
        var resetPage = await client.GetStringAsync(resetUrl);
        var code = ExtractInputValue(resetPage, "Input.Code");

        var resetResponse = await client.PostAsync(
            "/Identity/Account/ResetPassword",
            Form(token,
                ("Input.Email", email),
                ("Input.Code", code),
                ("Input.Password", NewPassword),
                ("Input.ConfirmPassword", NewPassword)));

        Assert.Equal(HttpStatusCode.Redirect, resetResponse.StatusCode);
        Assert.Equal(
            "/Identity/Account/ResetPasswordConfirmation",
            resetResponse.Headers.Location?.OriginalString);
        Assert.Equal(
            HttpStatusCode.Redirect,
            (await LoginAsync(client, email, NewPassword)).StatusCode);
        await WithUserManagerAsync(async manager =>
        {
            var user = await manager.FindByEmailAsync(email);
            Assert.NotNull(user);
            Assert.False(await manager.CheckPasswordAsync(user, Password));
            Assert.True(await manager.CheckPasswordAsync(user, NewPassword));
        });
    }

    [Fact]
    public async Task LoginPageLinksToForgotPassword()
    {
        using var client = CreateClient();

        var html = await client.GetStringAsync("/Identity/Account/Login");

        Assert.Contains(
            "href=\"/Identity/Account/ForgotPassword\"",
            html);
        Assert.Contains("Forgot your password?", html);
    }

    [Fact]
    public async Task UnverifiedAccountCanLoginAndBrowse()
    {
        var email = UniqueEmail("unverified-login");
        await CreateUserAsync(email, emailConfirmed: false);
        using var client = CreateClient();

        var login = await LoginAsync(client, email, Password);
        var page = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains(
            "Verify your email before creating or changing content",
            html);
        Assert.Contains("data-verification-toast", html);
        var fingerprint = Regex.Match(
            html,
            "data-verification-fingerprint=\"([^\"]+)\"");
        Assert.True(fingerprint.Success);
        Assert.DoesNotContain(email, fingerprint.Groups[1].Value);
        Assert.DoesNotContain(
            "<div class=\"container mt-3\">",
            html);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync("/js/site.js")).StatusCode);
    }

    [Fact]
    public async Task ConfirmationAndPasswordResetEmailsHaveSeparateCooldowns()
    {
        var email = UniqueEmail("cooldown");
        await CreateUserAsync(email, emailConfirmed: false);
        using var client = CreateClient();

        await PostEmailAsync(
            client,
            "/Identity/Account/ResendEmailConfirmation",
            email);
        await PostEmailAsync(
            client,
            "/Identity/Account/ResendEmailConfirmation",
            email);
        await PostEmailAsync(
            client,
            "/Identity/Account/ForgotPassword",
            email);
        await PostEmailAsync(
            client,
            "/Identity/Account/ForgotPassword",
            email);

        Assert.Single(_factory.EmailSender.Messages.Where(message =>
            message.Recipient == email &&
            message.Subject.Contains("Confirm")));
        Assert.Single(_factory.EmailSender.Messages.Where(message =>
            message.Recipient == email &&
            message.Subject.Contains("Reset")));
    }

    [Fact]
    public async Task EmailChangeRevokesVerificationAndSendsConfirmation()
    {
        var oldEmail = UniqueEmail("old-profile");
        var newEmail = UniqueEmail("new-profile");
        await CreateUserAsync(oldEmail, emailConfirmed: true);
        string? oldFingerprint = null;
        await WithUserManagerAsync(async manager =>
        {
            var user = await manager.FindByEmailAsync(oldEmail);
            Assert.NotNull(user);
            oldFingerprint = VerificationNoticeFingerprint.Create(user);
        });
        using var client = CreateClient();
        await LoginAsync(client, oldEmail, Password);
        var token = await GetAntiforgeryTokenAsync(
            client,
            "/Identity/Account/Manage");

        var response = await client.PostAsync(
            "/Identity/Account/Manage",
            Form(token,
                ("Input.DisplayName", "Changed Email User"),
                ("Input.Email", newEmail)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        string? newFingerprint = null;
        await WithUserManagerAsync(async manager =>
        {
            var user = await manager.FindByEmailAsync(newEmail);
            Assert.NotNull(user);
            Assert.False(user.EmailConfirmed);
            newFingerprint = VerificationNoticeFingerprint.Create(user);
        });
        Assert.NotEqual(oldFingerprint, newFingerprint);
        var homeHtml = await client.GetStringAsync("/");
        Assert.Contains(
            $"data-verification-fingerprint=\"{newFingerprint}\"",
            homeHtml);
        Assert.Single(_factory.EmailSender.Messages.Where(message =>
            message.Recipient == newEmail &&
            message.Subject.Contains("Confirm")));
    }

    [Fact]
    public async Task ManageEmailShowsCurrentVerificationStatus()
    {
        var email = UniqueEmail("manage-status");
        await CreateUserAsync(email, emailConfirmed: false);
        using var client = CreateClient();
        await LoginAsync(client, email, Password);

        var html = await client.GetStringAsync(
            "/Identity/Account/Manage/Email");

        Assert.Contains(email, html);
        Assert.Contains("Not verified", html);
        Assert.Contains("Resend verification email", html);
    }

    [Fact]
    public async Task RegistrationRateLimitRejectsFourthPostFromSameIp()
    {
        using var factory = new TravelBlogWebApplicationFactory();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true
            });

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var accepted = await RegisterAsync(
                client,
                UniqueEmail($"limited-{attempt}"));
            Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        }

        var rejected = await RegisterAsync(client, UniqueEmail("limited-last"));
        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            rejected.StatusCode);
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });

    private static async Task<HttpResponseMessage> RegisterAsync(
        HttpClient client,
        string email)
    {
        var token = await GetAntiforgeryTokenAsync(
            client,
            "/Identity/Account/Register");
        return await client.PostAsync(
            "/Identity/Account/Register",
            Form(token,
                ("Input.DisplayName", "Email Test User"),
                ("Input.Email", email),
                ("Input.Password", Password),
                ("Input.ConfirmPassword", Password)));
    }

    private static async Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string email,
        string password)
    {
        var token = await GetAntiforgeryTokenAsync(
            client,
            "/Identity/Account/Login");
        return await client.PostAsync(
            "/Identity/Account/Login",
            Form(token,
                ("Input.Email", email),
                ("Input.Password", password),
                ("Input.RememberMe", "false")));
    }

    private static async Task<HttpResponseMessage> PostEmailAsync(
        HttpClient client,
        string path,
        string email)
    {
        var token = await GetAntiforgeryTokenAsync(client, path);
        return await client.PostAsync(
            path,
            Form(token, ("Input.Email", email)));
    }

    private async Task CreateConfirmedUserAsync(string email)
        => await CreateUserAsync(email, emailConfirmed: true);

    private async Task CreateUserAsync(string email, bool emailConfirmed)
    {
        await WithUserManagerAsync(async manager =>
        {
            var user = new ApplicationUser
            {
                DisplayName = "Confirmed User",
                UserName = email,
                Email = email,
                EmailConfirmed = emailConfirmed
            };
            Assert.True((await manager.CreateAsync(user, Password)).Succeeded);
        });
    }

    private async Task WithUserManagerAsync(
        Func<UserManager<ApplicationUser>, Task> action)
    {
        using var scope = _factory.Services.CreateScope();
        await action(scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>());
    }

    private static async Task<string> GetAntiforgeryTokenAsync(
        HttpClient client,
        string path)
    {
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        return ExtractInputValue(html, "__RequestVerificationToken");
    }

    private static string ExtractInputValue(string html, string name)
    {
        var match = Regex.Match(
            html,
            $"name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, $"Input '{name}' was not found.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static string ExtractLink(string html)
    {
        var match = Regex.Match(html, "href=\"([^\"]+)\"");
        Assert.True(match.Success, "Email link was not found.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static FormUrlEncodedContent Form(
        string token,
        params (string Key, string Value)[] values)
    {
        var fields = values
            .Select(value =>
                new KeyValuePair<string, string>(value.Key, value.Value))
            .Append(new KeyValuePair<string, string>(
                "__RequestVerificationToken",
                token));
        return new FormUrlEncodedContent(fields);
    }

    private static string UniqueEmail(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}@example.test";
}
