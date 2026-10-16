namespace TravelBlog.Web.Models;

public static class ClubBookTimeline
{
    public const string Current = "Current";
    public const string Upcoming = "Upcoming";
    public const string Past = "Past";

    public static ClubBook? CurrentBook(
        IEnumerable<ClubBook>? books,
        DateTime utcNow)
    {
        if (books is null)
        {
            return null;
        }

        var today = utcNow.Date;
        return books
            .Where(book =>
                book.StartDate.Date <= today &&
                book.EndDate.Date >= today)
            .OrderBy(book => book.EndDate)
            .ThenBy(book => book.Id)
            .FirstOrDefault();
    }

    public static string Status(ClubBook book, DateTime utcNow)
    {
        var today = utcNow.Date;
        if (book.EndDate.Date < today)
        {
            return Past;
        }

        if (book.StartDate.Date > today)
        {
            return Upcoming;
        }

        return Current;
    }
}
