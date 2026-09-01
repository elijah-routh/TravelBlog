using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TravelBlog.Web.Data;
using TravelBlog.Web.Models;

namespace TravelBlog.Web.Services;

public enum AccountAnonymizationStatus
{
    Succeeded,
    Forbidden,
    ProtectedAccount,
    NotFound,
    Failed
}

public sealed record AccountAnonymizationResult(
    AccountAnonymizationStatus Status,
    int Posts = 0,
    int Comments = 0,
    int Discussions = 0,
    int Notices = 0,
    int Clubs = 0,
    int Memberships = 0,
    int Votes = 0);

public interface IAccountAnonymizationService
{
    Task<AccountAnonymizationResult> AnonymizeAsync(
        string actorId,
        string targetId,
        CancellationToken cancellationToken = default);

    Task<AccountAnonymizationResult> AnonymizeSelfAsync(
        string userId,
        CancellationToken cancellationToken = default);
}

public sealed class AccountAnonymizationService(
    BlogDbContext context,
    UserManager<ApplicationUser> userManager,
    ILogger<AccountAnonymizationService> logger)
    : IAccountAnonymizationService
{
    public async Task<AccountAnonymizationResult> AnonymizeAsync(
        string actorId,
        string targetId,
        CancellationToken cancellationToken = default)
    {
        if (actorId != BootstrapAdminConstants.UserId)
        {
            return new(AccountAnonymizationStatus.Forbidden);
        }

        if (targetId == actorId ||
            targetId == BootstrapAdminConstants.UserId ||
            targetId == DeletedUserConstants.UserId)
        {
            return new(AccountAnonymizationStatus.ProtectedAccount);
        }

        return await ExecuteAnonymizationAsync(
            actorId,
            targetId,
            cancellationToken);
    }

    public async Task<AccountAnonymizationResult> AnonymizeSelfAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == BootstrapAdminConstants.UserId ||
            userId == DeletedUserConstants.UserId)
        {
            return new(AccountAnonymizationStatus.ProtectedAccount);
        }

        return await ExecuteAnonymizationAsync(
            userId,
            userId,
            cancellationToken);
    }

    private async Task<AccountAnonymizationResult> ExecuteAnonymizationAsync(
        string actorId,
        string targetId,
        CancellationToken cancellationToken)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await context.Database.BeginTransactionAsync(cancellationToken);
            var target = await userManager.FindByIdAsync(targetId);
            if (target is null)
            {
                return new AccountAnonymizationResult(
                    AccountAnonymizationStatus.NotFound);
            }

            var sentinelExists = await context.Users.AnyAsync(
                user => user.Id == DeletedUserConstants.UserId,
                cancellationToken);
            if (!sentinelExists)
            {
                throw new InvalidOperationException(
                    "The deleted-user sentinel is missing.");
            }

            var posts = await context.Posts
                .Where(post => post.AuthorId == targetId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        post => post.AuthorId,
                        DeletedUserConstants.UserId),
                    cancellationToken);
            var comments = await context.PostComments
                .Where(comment => comment.AuthorId == targetId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        comment => comment.AuthorId,
                        DeletedUserConstants.UserId),
                    cancellationToken);
            var discussions = await context.DiscussionPosts
                .Where(post => post.AuthorId == targetId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        post => post.AuthorId,
                        DeletedUserConstants.UserId),
                    cancellationToken);
            var notices = await context.ClubNotices
                .Where(notice => notice.AuthorId == targetId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        notice => notice.AuthorId,
                        DeletedUserConstants.UserId),
                    cancellationToken);
            var clubs = await context.BookClubs
                .Where(club => club.CreatedById == targetId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        club => club.CreatedById,
                        DeletedUserConstants.UserId),
                    cancellationToken);
            var memberships = await context.BookClubMemberships
                .Where(membership => membership.UserId == targetId)
                .ExecuteDeleteAsync(cancellationToken);
            var votes = await context.DiscussionPollVotes
                .Where(vote => vote.UserId == targetId)
                .ExecuteDeleteAsync(cancellationToken);
            await context.PostLikes
                .Where(like => like.UserId == targetId)
                .ExecuteDeleteAsync(cancellationToken);
            await context.PostCommentLikes
                .Where(like => like.UserId == targetId)
                .ExecuteDeleteAsync(cancellationToken);
            await context.DiscussionPostLikes
                .Where(like => like.UserId == targetId)
                .ExecuteDeleteAsync(cancellationToken);
            await context.PostViews
                .Where(view => view.UserId == targetId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        view => view.UserId,
                        (string?)null),
                    cancellationToken);

            var identityResult = await userManager.DeleteAsync(target);
            if (!identityResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                logger.LogWarning(
                    "Account anonymization failed for actor {ActorId} and " +
                    "target {TargetId} during Identity deletion.",
                    actorId,
                    targetId);
                return new AccountAnonymizationResult(
                    AccountAnonymizationStatus.Failed);
            }

            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation(
                "Account anonymized by actor {ActorId}: target {TargetId}; " +
                "posts {Posts}, comments {Comments}, discussions " +
                "{Discussions}, notices {Notices}, clubs {Clubs}, " +
                "memberships {Memberships}, votes {Votes}.",
                actorId,
                targetId,
                posts,
                comments,
                discussions,
                notices,
                clubs,
                memberships,
                votes);
            return new AccountAnonymizationResult(
                AccountAnonymizationStatus.Succeeded,
                posts,
                comments,
                discussions,
                notices,
                clubs,
                memberships,
                votes);
        });
    }
}
