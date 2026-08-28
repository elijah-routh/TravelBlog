using System.Collections.Concurrent;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace TravelBlog.Web.Services;

public sealed class ApplicationRateLimitOptions
{
    public const string SectionName = "RateLimits";

    public int LoginPermitLimit { get; set; } = 15;
    public int CommentPermitLimit { get; set; } = 10;
    public int ImageUploadPermitLimit { get; set; } = 5;
}

public interface IImageUploadRateLimiter
{
    RateLimitLease Acquire(HttpContext context);
}

public sealed class ImageUploadRateLimiter(
    IOptions<ApplicationRateLimitOptions> options) :
    IImageUploadRateLimiter,
    IDisposable
{
    private readonly ConcurrentDictionary<
        string,
        Lazy<FixedWindowRateLimiter>> _limiters = new();

    public RateLimitLease Acquire(HttpContext context)
    {
        var userId = context.User.FindFirstValue(
            ClaimTypes.NameIdentifier);
        var partition = !string.IsNullOrWhiteSpace(userId)
            ? $"user:{userId}"
            : $"ip:{context.Connection.RemoteIpAddress}";
        var limiter = _limiters.GetOrAdd(
            partition,
            _ => new Lazy<FixedWindowRateLimiter>(
                () => new FixedWindowRateLimiter(
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.Value.ImageUploadPermitLimit,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }),
                LazyThreadSafetyMode.ExecutionAndPublication));
        return limiter.Value.AttemptAcquire();
    }

    public void Dispose()
    {
        foreach (var limiter in _limiters.Values)
        {
            if (limiter.IsValueCreated)
            {
                limiter.Value.Dispose();
            }
        }
    }
}
