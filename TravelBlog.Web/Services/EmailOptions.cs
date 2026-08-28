using System.ComponentModel.DataAnnotations;
using MailKit.Security;

namespace TravelBlog.Web.Services;

public sealed class EmailOptions : IValidatableObject
{
    public const string SectionName = "Email";

    [Required]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    [Required]
    [EmailAddress]
    public string FromAddress { get; set; } = string.Empty;

    [Required]
    public string FromName { get; set; } = string.Empty;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public SecureSocketOptions SocketOptions { get; set; } =
        SecureSocketOptions.StartTls;

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Username) !=
            string.IsNullOrWhiteSpace(Password))
        {
            yield return new ValidationResult(
                "Email:Username and Email:Password must either both be set " +
                "or both be omitted.",
                [nameof(Username), nameof(Password)]);
        }

        if (SocketOptions is not SecureSocketOptions.StartTls and
            not SecureSocketOptions.SslOnConnect)
        {
            yield return new ValidationResult(
                "Email:SocketOptions must be StartTls or SslOnConnect.",
                [nameof(SocketOptions)]);
        }
    }
}
