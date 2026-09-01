using Amazon.Runtime;
using Amazon.S3;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.RateLimiting;
using TravelBlog.Web.Authorization;
using TravelBlog.Web.Data;
using TravelBlog.Web.Models;
using TravelBlog.Web.Services;

var builder = WebApplication.CreateBuilder(args);

var dataProtection = builder.Services
    .AddDataProtection()
    .SetApplicationName("TravelBlog");
var dataProtectionKeysPath =
    builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    Directory.CreateDirectory(dataProtectionKeysPath);
    dataProtection.PersistKeysToFileSystem(
        new DirectoryInfo(dataProtectionKeysPath));
}

builder.Services.AddControllersWithViews(options =>
    options.Filters.Add<VerifiedMutationFilter>());
builder.Services.AddRazorPages();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IAccountEmailService, AccountEmailService>();
builder.Services.AddScoped<
    IAccountAnonymizationService,
    AccountAnonymizationService>();
builder.Services
    .AddOptions<ApplicationRateLimitOptions>()
    .Bind(builder.Configuration.GetSection(
        ApplicationRateLimitOptions.SectionName));
builder.Services.AddSingleton<
    IImageUploadRateLimiter,
    ImageUploadRateLimiter>();
builder.Services.AddSingleton<INutPlaceholderImages, NutPlaceholderImages>();

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

    builder.Services
        .AddOptions<EmailOptions>()
        .Bind(builder.Configuration.GetSection(EmailOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();
    builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
}

builder.Services
    .AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(15);
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
    options.AddPolicy(
        PolicyNames.VerifiedEmail,
        policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new VerifiedEmailRequirement()));
    options.AddPolicy(
        PolicyNames.ActiveAuthor,
        policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new VerifiedEmailRequirement())
            .AddRequirements(new BlockedAccountRequirement()));
    options.AddPolicy(
        PolicyNames.BootstrapAdmin,
        policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new BootstrapAdminRequirement()));
});

builder.Services.AddScoped<
    IAuthorizationHandler,
    PostOwnerOrAdminHandler>();
builder.Services.AddScoped<
    IAuthorizationHandler,
    VerifiedEmailHandler>();
builder.Services.AddScoped<
    IAuthorizationHandler,
    BlockedAccountHandler>();
builder.Services.AddSingleton<
    IAuthorizationHandler,
    BootstrapAdminHandler>();
builder.Services.AddScoped<VerifiedMutationFilter>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType =
            "text/plain; charset=utf-8";
        await context.HttpContext.Response.WriteAsync(
            "Too many requests. Please try again later.",
            cancellationToken);
    };
    var limits = new ApplicationRateLimitOptions();
    builder.Configuration.GetSection(
        ApplicationRateLimitOptions.SectionName).Bind(limits);
    options.AddPolicy(RateLimitPolicyNames.Registration, context =>
        !HttpMethods.IsPost(context.Request.Method)
            ? RateLimitPartition.GetNoLimiter("registration-read")
            : RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0
            }));
    options.AddPolicy(RateLimitPolicyNames.Email, context =>
        !HttpMethods.IsPost(context.Request.Method)
            ? RateLimitPartition.GetNoLimiter("email-read")
            : RateLimitPartition.GetFixedWindowLimiter(
            $"{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}:" +
            context.Request.Path.Value?.ToLowerInvariant(),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0
            }));
    options.AddPolicy(RateLimitPolicyNames.Login, context =>
        !HttpMethods.IsPost(context.Request.Method)
            ? RateLimitPartition.GetNoLimiter("login-read")
            : RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limits.LoginPermitLimit,
                    Window = TimeSpan.FromMinutes(15),
                    QueueLimit = 0
                }));
    options.AddPolicy(RateLimitPolicyNames.Comments, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirstValue(ClaimTypes.NameIdentifier) is
                { Length: > 0 } userId
                ? $"user:{userId}"
                : $"ip:{context.Connection.RemoteIpAddress}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limits.CommentPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

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
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.MapRazorPages();

app.Run();

public partial class Program;