using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TravelBlog.Web.Models;

namespace TravelBlog.Web.Data;

public class BlogDbContext : IdentityDbContext<ApplicationUser>
{
    public BlogDbContext(DbContextOptions<BlogDbContext> options)
        : base(options)
    {
    }

    public DbSet<Post> Posts => Set<Post>();

    public DbSet<BookClub> BookClubs => Set<BookClub>();

    public DbSet<BookClubMembership> BookClubMemberships =>
        Set<BookClubMembership>();

    public DbSet<ClubBook> ClubBooks => Set<ClubBook>();

    public DbSet<BookDiscussionThread> BookDiscussionThreads =>
        Set<BookDiscussionThread>();

    public DbSet<ClubNotice> ClubNotices => Set<ClubNotice>();

    public DbSet<DiscussionPost> DiscussionPosts => Set<DiscussionPost>();

    public DbSet<DiscussionPoll> DiscussionPolls => Set<DiscussionPoll>();

    public DbSet<DiscussionPollOption> DiscussionPollOptions =>
        Set<DiscussionPollOption>();

    public DbSet<DiscussionPollVote> DiscussionPollVotes =>
        Set<DiscussionPollVote>();

    public DbSet<PostComment> PostComments => Set<PostComment>();

    public DbSet<PostLike> PostLikes => Set<PostLike>();

    public DbSet<PostCommentLike> PostCommentLikes => Set<PostCommentLike>();

    public DbSet<DiscussionPostLike> DiscussionPostLikes =>
        Set<DiscussionPostLike>();

    public DbSet<PostView> PostViews => Set<PostView>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>().HasData(new ApplicationUser
        {
            Id = DeletedUserConstants.UserId,
            DisplayName = DeletedUserConstants.DisplayName,
            ConcurrencyStamp = "deleted-user-sentinel-concurrency",
            SecurityStamp = "deleted-user-sentinel-security",
            Email = null,
            NormalizedEmail = null,
            UserName = null,
            NormalizedUserName = null,
            EmailConfirmed = false,
            PhoneNumber = null,
            PhoneNumberConfirmed = false,
            PasswordHash = null,
            TwoFactorEnabled = false,
            LockoutEnabled = false,
            AccessFailedCount = 0
        });

        modelBuilder.Entity<Post>()
            .HasIndex(post => post.Slug)
            .IsUnique();

        modelBuilder.Entity<Post>()
            .HasOne(post => post.Author)
            .WithMany(user => user.Posts)
            .HasForeignKey(post => post.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<BookClub>(entity =>
        {
            entity.HasIndex(club => club.Slug).IsUnique();

            entity.HasOne(club => club.CreatedBy)
                .WithMany(user => user.CreatedBookClubs)
                .HasForeignKey(club => club.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BookClubMembership>(entity =>
        {
            entity.HasKey(membership => new
            {
                membership.ClubId,
                membership.UserId
            });

            entity.HasOne(membership => membership.Club)
                .WithMany(club => club.Memberships)
                .HasForeignKey(membership => membership.ClubId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(membership => membership.User)
                .WithMany(user => user.BookClubMemberships)
                .HasForeignKey(membership => membership.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ClubBook>(entity =>
        {
            entity.HasOne(book => book.Club)
                .WithMany(club => club.Books)
                .HasForeignKey(book => book.ClubId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BookDiscussionThread>(entity =>
        {
            entity.HasIndex(thread => new
            {
                thread.ClubBookId,
                thread.Title
            }).IsUnique();

            entity.HasOne(thread => thread.ClubBook)
                .WithMany(book => book.DiscussionThreads)
                .HasForeignKey(thread => thread.ClubBookId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ClubNotice>(entity =>
        {
            entity.HasOne(notice => notice.Club)
                .WithMany(club => club.Notices)
                .HasForeignKey(notice => notice.ClubId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(notice => notice.Author)
                .WithMany(user => user.ClubNotices)
                .HasForeignKey(notice => notice.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DiscussionPost>(entity =>
        {
            entity.HasOne(post => post.Club)
                .WithMany(club => club.DiscussionPosts)
                .HasForeignKey(post => post.ClubId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(post => post.ClubBook)
                .WithMany(book => book.DiscussionPosts)
                .HasForeignKey(post => post.ClubBookId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(post => post.BookDiscussionThread)
                .WithMany(thread => thread.Posts)
                .HasForeignKey(post => post.BookDiscussionThreadId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(post => post.Author)
                .WithMany(user => user.DiscussionPosts)
                .HasForeignKey(post => post.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(post => post.Parent)
                .WithMany(post => post.Replies)
                .HasForeignKey(post => post.ParentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DiscussionPoll>(entity =>
        {
            entity.HasIndex(poll => poll.DiscussionPostId).IsUnique();

            entity.HasOne(poll => poll.DiscussionPost)
                .WithOne(post => post.Poll)
                .HasForeignKey<DiscussionPoll>(poll => poll.DiscussionPostId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DiscussionPollOption>(entity =>
        {
            entity.HasOne(option => option.Poll)
                .WithMany(poll => poll.Options)
                .HasForeignKey(option => option.PollId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DiscussionPollVote>(entity =>
        {
            entity.HasIndex(vote => new { vote.PollId, vote.UserId })
                .IsUnique();

            entity.HasOne(vote => vote.Option)
                .WithMany(option => option.Votes)
                .HasForeignKey(vote => vote.OptionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(vote => vote.User)
                .WithMany(user => user.DiscussionPollVotes)
                .HasForeignKey(vote => vote.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PostComment>(entity =>
        {
            entity.HasOne(comment => comment.Post)
                .WithMany(post => post.Comments)
                .HasForeignKey(comment => comment.PostId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(comment => comment.Author)
                .WithMany(user => user.PostComments)
                .HasForeignKey(comment => comment.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(comment => comment.Parent)
                .WithMany(comment => comment.Replies)
                .HasForeignKey(comment => comment.ParentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PostLike>(entity =>
        {
            entity.HasIndex(like => new { like.PostId, like.UserId })
                .IsUnique();

            entity.HasOne(like => like.Post)
                .WithMany(post => post.Likes)
                .HasForeignKey(like => like.PostId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(like => like.User)
                .WithMany(user => user.PostLikes)
                .HasForeignKey(like => like.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PostCommentLike>(entity =>
        {
            entity.HasIndex(like => new { like.PostCommentId, like.UserId })
                .IsUnique();

            entity.HasOne(like => like.PostComment)
                .WithMany(comment => comment.Likes)
                .HasForeignKey(like => like.PostCommentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(like => like.User)
                .WithMany(user => user.PostCommentLikes)
                .HasForeignKey(like => like.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DiscussionPostLike>(entity =>
        {
            entity.HasIndex(like => new { like.DiscussionPostId, like.UserId })
                .IsUnique();

            entity.HasOne(like => like.DiscussionPost)
                .WithMany(post => post.Likes)
                .HasForeignKey(like => like.DiscussionPostId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(like => like.User)
                .WithMany(user => user.DiscussionPostLikes)
                .HasForeignKey(like => like.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PostView>(entity =>
        {
            entity.HasIndex(view => new { view.PostId, view.ViewerKey })
                .IsUnique();

            entity.HasOne(view => view.Post)
                .WithMany(post => post.Views)
                .HasForeignKey(view => view.PostId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(view => view.User)
                .WithMany(user => user.PostViews)
                .HasForeignKey(view => view.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}