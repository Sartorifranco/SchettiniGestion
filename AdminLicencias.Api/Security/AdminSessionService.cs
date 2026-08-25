using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace AdminLicencias.Api.Security;

public sealed class AdminSessionTicket
{
    public string UserIdentifier { get; set; } = "";
    public DateTimeOffset ExpiresUtc { get; set; }
}

public sealed class AdminSessionService
{
    private readonly IDataProtector _protector;
    private readonly ApiSecurityOptions _options;

    public AdminSessionService(IDataProtectionProvider provider, IOptions<ApiSecurityOptions> options)
    {
        _protector = provider.CreateProtector("SCHPOS.AdminLicencias.Session.v1");
        _options = options.Value;
    }

    public string CreateCookieValue(string userIdentifier)
    {
        var ticket = new AdminSessionTicket
        {
            UserIdentifier = userIdentifier.Trim(),
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(Math.Clamp(_options.SessionHours, 1, 72))
        };
        string json = JsonSerializer.Serialize(ticket);
        byte[] protectedBytes = _protector.Protect(Encoding.UTF8.GetBytes(json));
        return Convert.ToBase64String(protectedBytes);
    }

    public AdminSessionTicket? TryRead(string? cookieValue)
    {
        if (string.IsNullOrWhiteSpace(cookieValue)) return null;
        try
        {
            byte[] protectedBytes = Convert.FromBase64String(cookieValue);
            byte[] bytes = _protector.Unprotect(protectedBytes);
            var ticket = JsonSerializer.Deserialize<AdminSessionTicket>(Encoding.UTF8.GetString(bytes));
            if (ticket == null || string.IsNullOrWhiteSpace(ticket.UserIdentifier))
                return null;
            if (ticket.ExpiresUtc < DateTimeOffset.UtcNow)
                return null;
            return ticket;
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    public CookieOptions BuildCookieOptions(HttpRequest request)
    {
        bool https = request.IsHttps
            || string.Equals(request.Headers["X-Forwarded-Proto"].ToString(), "https", StringComparison.OrdinalIgnoreCase);

        return new CookieOptions
        {
            HttpOnly = true,
            Secure = https,
            SameSite = SameSiteMode.Strict,
            IsEssential = true,
            MaxAge = TimeSpan.FromHours(Math.Clamp(_options.SessionHours, 1, 72)),
            Path = "/"
        };
    }
}
