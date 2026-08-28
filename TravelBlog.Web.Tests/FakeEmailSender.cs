using System.Collections.Concurrent;
using TravelBlog.Web.Services;

namespace TravelBlog.Web.Tests;

public sealed record SentEmail(
    string Recipient,
    string Subject,
    string HtmlBody);

public sealed class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<SentEmail> _messages = new();

    public IReadOnlyCollection<SentEmail> Messages => _messages.ToArray();

    public Task SendEmailAsync(
        string recipient,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        _messages.Enqueue(new SentEmail(recipient, subject, htmlBody));
        return Task.CompletedTask;
    }
}
