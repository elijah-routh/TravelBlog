namespace TravelBlog.Web.Services;

public interface IEmailSender
{
    Task SendEmailAsync(
        string recipient,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default);
}
