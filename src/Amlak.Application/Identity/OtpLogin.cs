using System.Security.Cryptography;
using Amlak.Application.Abstractions;

namespace Amlak.Application.Identity;

public enum OtpRequest { Sent, TooSoon, Failed, InvalidPhone }

/// <summary>
/// Login with a one-time SMS code: request a code for a mobile number, then trade the code for the verified
/// number. Keeping the session (cookie) is the web layer's job; this use case only decides who proved what.
/// </summary>
public sealed class OtpLogin(IPhoneCodeRepository codes, ISmsSender sms, IUnitOfWork uow, TimeProvider clock)
{
    /// <summary>A number gets at most one new code in this window.</summary>
    public static readonly TimeSpan ResendAfter = TimeSpan.FromMinutes(2);

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<OtpRequest> RequestCodeAsync(string? mobile, CancellationToken ct = default)
    {
        if (!PhoneNumber.TryParse(mobile, out var phone)) return OtpRequest.InvalidPhone;
        var now = Now;
        if (await codes.IssuedSinceAsync(phone.Value, now - ResendAfter, ct)) return OtpRequest.TooSoon;

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        codes.Add(PhoneCode.Issue(phone, code, now));
        // Saved before sending: a crash after the SMS still counts against the resend window.
        await uow.SaveChangesAsync(ct);
        return await sms.SendCodeAsync(phone.Value, code, ct) ? OtpRequest.Sent : OtpRequest.Failed;
    }

    /// <returns>The verified number as 09xxxxxxxxx, or a Persian error to show.</returns>
    public async Task<Result<string>> VerifyAsync(string? mobile, string? code, CancellationToken ct = default)
    {
        if (!PhoneNumber.TryParse(mobile, out var phone)) return Result<string>.Fail("شمارهٔ موبایل معتبر نیست.");
        var now = Now;
        if (await codes.LatestOpenAsync(phone.Value, now, ct) is not { } entry)
            return Result<string>.Fail("کدی برای این شماره فرستاده نشده یا منقضی شده است.");

        var check = entry.Verify(code, now);
        if (check is OtpCheck.Verified or OtpCheck.Wrong) await uow.SaveChangesAsync(ct);
        return check switch
        {
            OtpCheck.Verified => phone.Value,
            OtpCheck.Wrong => Result<string>.Fail("کد درست نیست."),
            OtpCheck.Locked => Result<string>.Fail("تعداد تلاش‌ها زیاد شد؛ کد تازه بگیرید."),
            _ => Result<string>.Fail("کدی برای این شماره فرستاده نشده یا منقضی شده است."),
        };
    }
}
