using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using TravelBlog.Web.Models;

namespace TravelBlog.Web.Services;

public static class VerificationNoticeFingerprint
{
    public static string Create(ApplicationUser user)
    {
        var normalizedEmail = user.NormalizedEmail ??
            user.Email?.Trim().ToUpperInvariant() ??
            string.Empty;
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{user.Id}\0{normalizedEmail}"));
        return WebEncoders.Base64UrlEncode(digest.AsSpan(0, 18));
    }
}
