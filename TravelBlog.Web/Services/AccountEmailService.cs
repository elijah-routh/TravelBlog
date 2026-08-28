using System.Text.Encodings.Web;
using Microsoft.EntityFrameworkCore;
using TravelBlog.Web.Data;
using TravelBlog.Web.Models;

namespace TravelBlog.Web.Services;

public interface IAccountEmailService
{
    Task<bool> SendConfirmationAsync(
        ApplicationUser user,
        string callbackUrl,
        CancellationToken cancellationToken = default);

    Task<bool> SendPasswordResetAsync(
        ApplicationUser user,
        string callbackUrl,
        CancellationToken cancellationToken = default);
}

public sealed class AccountEmailService(
    BlogDbContext context,
    IEmailSender emailSender,
    TimeProvider timeProvider,
    ILogger<AccountEmailService> logger) : IAccountEmailService
{
    public static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(5);

    public async Task<bool> SendConfirmationAsync(
        ApplicationUser user,
        string callbackUrl,
        CancellationToken cancellationToken = default)
    {
        var acquiredAt = timeProvider.GetUtcNow().UtcDateTime;
        var acquired = await context.Users
            .Where(candidate =>
                candidate.Id == user.Id &&
                (candidate.LastConfirmationEmailSentAt == null ||
                 candidate.LastConfirmationEmailSentAt <= acquiredAt - Cooldown))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    candidate => candidate.LastConfirmationEmailSentAt,
                    acquiredAt),
                cancellationToken) == 1;
        if (!acquired)
        {
            return false;
        }

        try
        {
            var encodedUrl = HtmlEncoder.Default.Encode(callbackUrl);
            await emailSender.SendEmailAsync(
                user.Email!,
                "Confirm your TravelBlog email",
                $"<p>Confirm your email address by " +
                $"<a href=\"{encodedUrl}\">clicking here</a>.</p>",
                cancellationToken);
            return true;
        }
        catch
        {
            await ReleaseConfirmationSlotAsync(user.Id, acquiredAt);
            throw;
        }
    }

    public async Task<bool> SendPasswordResetAsync(
        ApplicationUser user,
        string callbackUrl,
        CancellationToken cancellationToken = default)
    {
        var acquiredAt = timeProvider.GetUtcNow().UtcDateTime;
        var acquired = await context.Users
            .Where(candidate =>
                candidate.Id == user.Id &&
                (candidate.LastPasswordResetEmailSentAt == null ||
                 candidate.LastPasswordResetEmailSentAt <= acquiredAt - Cooldown))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    candidate => candidate.LastPasswordResetEmailSentAt,
                    acquiredAt),
                cancellationToken) == 1;
        if (!acquired)
        {
            return false;
        }

        try
        {
            var encodedUrl = HtmlEncoder.Default.Encode(callbackUrl);
            await emailSender.SendEmailAsync(
                user.Email!,
                "Reset your TravelBlog password",
                $"<p>Reset your password by " +
                $"<a href=\"{encodedUrl}\">clicking here</a>.</p>",
                cancellationToken);
            return true;
        }
        catch
        {
            await ReleasePasswordResetSlotAsync(user.Id, acquiredAt);
            throw;
        }
    }

    private async Task ReleaseConfirmationSlotAsync(
        string userId,
        DateTime acquiredAt)
    {
        try
        {
            await context.Users
                .Where(user =>
                    user.Id == userId &&
                    user.LastConfirmationEmailSentAt == acquiredAt)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    user => user.LastConfirmationEmailSentAt,
                    (DateTime?)null));
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not release confirmation email slot for {UserId}.",
                userId);
        }
    }

    private async Task ReleasePasswordResetSlotAsync(
        string userId,
        DateTime acquiredAt)
    {
        try
        {
            await context.Users
                .Where(user =>
                    user.Id == userId &&
                    user.LastPasswordResetEmailSentAt == acquiredAt)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    user => user.LastPasswordResetEmailSentAt,
                    (DateTime?)null));
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not release password reset email slot for {UserId}.",
                userId);
        }
    }
}
