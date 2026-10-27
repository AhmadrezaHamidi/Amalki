using System.Security.Cryptography;
using System.Text;
using Amlak.Web.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Services.Sms;

public enum SendResult { Sent, TooSoon, Failed }

/// <summary>
/// SMS one-time codes for visitors who submit listings: 6 digits, valid 5 minutes, 5 tries, one new code
/// per phone every 2 minutes. A verified phone is kept in a signed, encrypted cookie for one hour.
/// </summary>
public class PhoneVerification(AppDbContext db, ISmsSender sms, IDataProtectionProvider dp, TimeProvider clock)
{
    public const string CookieName = "ash_owner";
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan Resend = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan Session = TimeSpan.FromHours(1);
    public const int MaxAttempts = 5;

    private readonly IDataProtector _protector = dp.CreateProtector("Amlak.OwnerPhone");
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<SendResult> SendAsync(string mobile, CancellationToken ct = default)
    {
        var since = Now - Resend;
        if (await db.PhoneCodes.AnyAsync(x => x.Phone == mobile && x.CreatedAtUtc > since, ct)) return SendResult.TooSoon;

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        db.PhoneCodes.Add(new PhoneCode { Phone = mobile, CodeHash = Hash(mobile, code), CreatedAtUtc = Now, ExpiresAtUtc = Now + CodeLifetime });
        await db.SaveChangesAsync(ct);
        return await sms.SendCodeAsync(mobile, code, ct) ? SendResult.Sent : SendResult.Failed;
    }

    public async Task<bool> VerifyAsync(string mobile, string code, CancellationToken ct = default)
    {
        var now = Now;
        var entry = await db.PhoneCodes.Where(x => x.Phone == mobile && !x.Used && x.ExpiresAtUtc > now)
            .OrderByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
        if (entry is null || entry.Attempts >= MaxAttempts) return false;

        var given = Fa.Latin(code ?? "").Trim();
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(entry.CodeHash), Encoding.ASCII.GetBytes(Hash(mobile, given))))
        {
            entry.Attempts++;
            await db.SaveChangesAsync(ct);
            return false;
        }
        entry.Used = true;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public void SignIn(HttpResponse response, string mobile) =>
        response.Cookies.Append(CookieName, _protector.Protect($"{mobile}|{(Now + Session).Ticks}"),
            new CookieOptions { HttpOnly = true, Secure = response.HttpContext.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = Session, IsEssential = true });

    public static void SignOut(HttpResponse response) => response.Cookies.Delete(CookieName);

    /// <returns>The verified mobile from the cookie, or null when missing, tampered with or expired.</returns>
    public string? CurrentPhone(HttpRequest request)
    {
        if (!request.Cookies.TryGetValue(CookieName, out var raw)) return null;
        try
        {
            var parts = _protector.Unprotect(raw).Split('|');
            return parts.Length == 2 && long.TryParse(parts[1], out var ticks) && ticks > Now.Ticks ? parts[0] : null;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }

    private static string Hash(string mobile, string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(mobile + ":" + code)));
}
