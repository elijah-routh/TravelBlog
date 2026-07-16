using Microsoft.EntityFrameworkCore;
using TravelBlog.Web.Models;

namespace TravelBlog.Web.Data;

public class BlogDbContext : DbContext
{
    public BlogDbContext(DbContextOptions<BlogDbContext> options)
        : base(options)
    {
    }

    public DbSet<Post> Posts => Set<Post>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Post>()
            .HasIndex(post => post.Slug)
            .IsUnique();
    }
}