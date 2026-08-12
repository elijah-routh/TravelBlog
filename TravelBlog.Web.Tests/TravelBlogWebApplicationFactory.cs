using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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

    public FakeImageStorage ImageStorage { get; } = new();

    public TravelBlogWebApplicationFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(_connection);
            services.AddSingleton<IImageStorage>(ImageStorage);
            services.AddDbContext<BlogDbContext>(options =>
                options.UseSqlite(_connection));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider
            .GetRequiredService<BlogDbContext>()
            .Database.EnsureCreated();

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
