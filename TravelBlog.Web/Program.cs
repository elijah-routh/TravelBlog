using Amazon.Runtime;
using Amazon.S3;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TravelBlog.Web.Authorization;
using TravelBlog.Web.Data;
using TravelBlog.Web.Models;
using TravelBlog.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

if (!builder.Environment.IsEnvironment("Testing"))
{
    var connectionString =
        builder.Configuration.GetConnectionString("BlogDatabase");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "The BlogDatabase connection string is missing."
        );
    }

    builder.Services.AddDbContext<BlogDbContext>(options =>
    {
        options.UseNpgsql(connectionString);
    });

    builder.Services
        .AddOptions<ObjectStorageOptions>()
        .Bind(builder.Configuration.GetSection(
            ObjectStorageOptions.SectionName))
        .ValidateDataAnnotations()
        .Validate(
            options => Uri.TryCreate(
                options.Endpoint,
                UriKind.Absolute,
                out _),
            "ObjectStorage:Endpoint must be an absolute URL.")
        .Validate(
            options => Uri.TryCreate(
                options.PublicBaseUrl,
                UriKind.Absolute,
                out _),
            "ObjectStorage:PublicBaseUrl must be an absolute URL.")
        .ValidateOnStart();

    builder.Services.AddSingleton<IAmazonS3>(serviceProvider =>
    {
        var options = serviceProvider
            .GetRequiredService<
                Microsoft.Extensions.Options.IOptions<
                    ObjectStorageOptions>>()
            .Value;

        var credentials = new BasicAWSCredentials(
            options.AccessKey,
            options.SecretKey);
        var configuration = new AmazonS3Config
        {
            ServiceURL = options.Endpoint,
            AuthenticationRegion = options.Region,
            ForcePathStyle = options.ForcePathStyle,
            RequestChecksumCalculation =
                RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation =
                ResponseChecksumValidation.WHEN_REQUIRED
        };

        return new AmazonS3Client(credentials, configuration);
    });
    builder.Services.AddSingleton<IImageStorage, S3ImageStorage>();
}

builder.Services
    .AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<BlogDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        PolicyNames.PostOwnerOrAdmin,
        policy => policy.AddRequirements(
            new PostOwnerOrAdminRequirement()));
});

builder.Services.AddScoped<
    IAuthorizationHandler,
    PostOwnerOrAdminHandler>();

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider
        .GetRequiredService<BlogDbContext>();

    await dbContext.Database.MigrateAsync();
    await BootstrapAdminInitializer.InitializeAsync(
        scope.ServiceProvider,
        app.Configuration);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.MapRazorPages();

app.Run();

public partial class Program;