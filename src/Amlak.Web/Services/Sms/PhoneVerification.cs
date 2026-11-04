using Amlak.Application.Identity;
using Microsoft.AspNetCore.DataProtection;

namespace Amlak.Web.Services.Sms;

public enum SendResult { Sent, TooSoon, Failed }

/// <summary>
/// The web side of OTP login (<see cref="OtpLogin"/> holds the rules): sends and checks codes, and keeps a
/// verified phone in a signed, encrypted cookie for <see cref="Session"/>. One cookie serves the login page,
/// posting a listing and SMS alerts, so a visitor proves the number once.
/// </summary>
public class PhoneVerification(OtpLogin otp, IDataProtectionProvider dp, TimeProvider clock)
{
    public const string CookieName = "ash_owner";
    public static readonly TimeSpan CodeLifetime = PhoneCode.Lifetime;
    public static readonly TimeSpan Resend = OtpLogin.ResendAfter;
    public static readonly TimeSpan Session = TimeSpan.FromDays(7);
    public const int MaxAttempts = PhoneCode.MaxAttempts;

    private readonly IDataProtector _protector = dp.CreateProtector("Amlak.OwnerPhone");
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<SendResult> SendAsync(string mobile, CancellationToken ct = default) =>
        await otp.RequestCodeAsync(mobile, ct) switch
        {
            OtpRequest.Sent => SendResult.Sent,
            OtpRequest.TooSoon => SendResult.TooSoon,
            _ => SendResult.Failed,
        };

    public async Task<bool> VerifyAsync(string mobile, string code, CancellationToken ct = default) =>
        (await otp.VerifyAsync(mobile, code, ct)).Succeeded;

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
}
