namespace TravelBlog.Web.Services;

public static class RelativeTimeFormatter
{
    public static string FormatAgo(DateTime timestampUtc, DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        var elapsed = now - timestampUtc.ToUniversalTime();
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        var minutes = (int)elapsed.TotalMinutes;
        if (minutes < 60)
        {
            return minutes == 1 ? "1 min ago" : $"{minutes} mins ago";
        }

        var hours = (int)elapsed.TotalHours;
        if (hours < 24)
        {
            return hours == 1 ? "1 hour ago" : $"{hours} hours ago";
        }

        var days = (int)elapsed.TotalDays;
        if (days < 7)
        {
            return days == 1 ? "1 day ago" : $"{days} days ago";
        }

        if (days < 30)
        {
            var weeks = days / 7;
            return weeks == 1 ? "1 week ago" : $"{weeks} weeks ago";
        }

        if (days < 365)
        {
            var months = days / 30;
            return months == 1 ? "1 month ago" : $"{months} months ago";
        }

        var years = days / 365;
        return years == 1 ? "1 year ago" : $"{years} years ago";
    }
}
