using System.Security.Cryptography;
using System.Text;
using Amlak.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Services.Sms;

public enum OtpRequestStatus { Sent, InvalidPhone, TooSoon, SendFailed }
public record OtpRequestResult(OtpRequestStatus Status, int RetryAfterSeconds = 0);

/// <summary>Five-digit SMS codes: 2-minute lifetime, one resend per minute, five guesses per code.</summary>
public class OtpService(AppDbContext db, SmsGateway sms, TimeProvider clock)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan ResendAfter = TimeSpan.FromMinutes(1);
    public const int MaxAttempts = 5;

    public async Task<OtpRequestResult> RequestAsync(string phone, CancellationToken ct = default)
    {
        if (PhoneNumber.Normalize(phone) is not { } normalized) return new(OtpRequestStatus.InvalidPhone);
        var now = clock.GetUtcNow().UtcDateTime;

        var last = await db.PhoneOtps.Where(x => x.Phone == normalized)
            .OrderByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
        if (last is not null && now - last.CreatedAtUtc < ResendAfter)
            return new(OtpRequestStatus.TooSoon, (int)Math.Ceiling((ResendAfter - (now - last.CreatedAtUtc)).TotalSeconds));

        var code = RandomNumberGenerator.GetInt32(10000, 100000).ToString();
        db.PhoneOtps.Add(new PhoneOtp
        {
            Phone = normalized, CodeHash = Hash(normalized, code), CreatedAtUtc = now, ExpiresAtUtc = now + Lifetime,
        });
        await db.SaveChangesAsync(ct);

        var text = $"کد تأیید آشیانه: {Fa.Digits(code)}\nاین کد را به کسی ندهید.";
        var sent = await sms.SendAsync(normalized, text, promotional: false, ct: ct);
        return new(sent.Ok ? OtpRequestStatus.Sent : OtpRequestStatus.SendFailed);
    }

    /// <summary>True once per code; wrong guesses count against the latest code only.</summary>
    public async Task<bool> VerifyAsync(string phone, string code, CancellationToken ct = default)
    {
        if (PhoneNumber.Normalize(phone) is not { } normalized) return false;
        var digits = ToAscii(code.Trim());
        var now = clock.GetUtcNow().UtcDateTime;

        var otp = await db.PhoneOtps.Where(x => x.Phone == normalized)
            .OrderByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
        if (otp is null || otp.ConsumedAtUtc is not null || otp.ExpiresAtUtc <= now || otp.Attempts >= MaxAttempts)
            return false;

        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(otp.CodeHash),
                Encoding.ASCII.GetBytes(Hash(normalized, digits))))
        {
            otp.Attempts++;
            await db.SaveChangesAsync(ct);
            return false;
        }
        otp.ConsumedAtUtc = now;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static string ToAscii(string s) => new(s.Select(c => c switch
    {
        >= '۰' and <= '۹' => (char)('0' + (c - '۰')),
        >= '٠' and <= '٩' => (char)('0' + (c - '٠')),
        _ => c,
    }).ToArray());

    private static string Hash(string phone, string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(phone + ":" + code)));
}
