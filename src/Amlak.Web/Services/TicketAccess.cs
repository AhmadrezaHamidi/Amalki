using Microsoft.AspNetCore.DataProtection;

namespace Amlak.Web.Services;

/// <summary>
/// Which support tickets this browser may open. A ticket holds the visitor's name and mobile, so its
/// tracking code alone is not enough: the browser must have created the ticket or looked it up with the
/// code and the mobile number. The list lives in an encrypted, HttpOnly cookie.
/// </summary>
public sealed class TicketAccess(IDataProtectionProvider dp)
{
    public const string Cookie = "amlak_tickets";
    private const int MaxCodes = 20;
    private readonly IDataProtector _protector = dp.CreateProtector("Amlak.TicketAccess.v1");

    public bool Has(HttpContext ctx, string code) => Codes(ctx.Request).Contains(code);

    public void Grant(HttpContext ctx, string code)
    {
        var codes = Codes(ctx.Request).Where(c => c != code).Prepend(code).Take(MaxCodes);
        ctx.Response.Cookies.Append(Cookie, _protector.Protect(string.Join(',', codes)), new CookieOptions
        {
            HttpOnly = true, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Lax, IsEssential = true,
            MaxAge = TimeSpan.FromDays(180),
        });
    }

    private List<string> Codes(HttpRequest req)
    {
        if (!req.Cookies.TryGetValue(Cookie, out var raw) || string.IsNullOrEmpty(raw)) return [];
        try
        {
            return [.. _protector.Unprotect(raw).Split(',', StringSplitOptions.RemoveEmptyEntries)];
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return []; // tampered, or keys rotated: track the ticket again
        }
    }
}
