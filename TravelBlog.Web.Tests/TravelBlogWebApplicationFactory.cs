using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TravelBlog.Web.Data;
using TravelBlog.Web.Services;

namespace TravelBlog.Web.Tests;

public sealed class TravelBlogWebApplicationFactory
    : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection =
        new("Data Source=:memory:");
    private readonly bool _useProductionContentRateLimits;

    public FakeImageStorage ImageStorage { get; } = new();
    public FakeEmailSender EmailSender { get; } = new();

    public TravelBlogWebApplicationFactory()
        : this(useProductionContentRateLimits: false)
    {
    }

    internal TravelBlogWebApplicationFactory(
        bool useProductionContentRateLimits)
    {
        _useProductionContentRateLimits =
            useProductionContentRateLimits;
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        if (!_useProductionContentRateLimits)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["RateLimits:LoginPermitLimit"] = "10000",
                        ["RateLimits:CommentPermitLimit"] = "10000",
                        ["RateLimits:ImageUploadPermitLimit"] = "10000"
                    }));
        }
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(_connection);
            services.AddSingleton<IImageStorage>(ImageStorage);
            services.AddSingleton<IEmailSender>(EmailSender);
            services.AddDbContext<BlogDbContext>(options =>
                options.UseSqlite(_connection));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<BlogDbContext>();
        context.Database.EnsureCreated();
        var sentinel = context.Users.Single(user =>
            user.Id == DeletedUserConstants.UserId);
        if (sentinel.DisplayName != DeletedUserConstants.DisplayName ||
            sentinel.Email is not null ||
            sentinel.UserName is not null ||
            sentinel.PasswordHash is not null)
        {
            throw new InvalidOperationException(
                "The deleted-user sentinel was not seeded correctly.");
        }

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection
    : ICollectionFixture<TravelBlogWebApplicationFactory>
{
    public const string Name = "TravelBlog integration tests";
}
