using System.Security.Cryptography;
using System.Text;

namespace Amlak.Domain;

/// <summary>What happened when someone typed a one-time code.</summary>
public enum OtpCheck { Verified, Wrong, Expired, Locked, Used }

/// <summary>
/// A one-time SMS code that proves a mobile number (OTP login). Only a hash of phone + code is kept;
/// a code is valid for <see cref="Lifetime"/>, can be used once, and locks after <see cref="MaxAttempts"/> wrong tries.
/// </summary>
public class PhoneCode
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    public const int MaxAttempts = 5;
    public const int Length = 6;

    private PhoneCode() { } // EF Core

    public int Id { get; private set; }
    public string Phone { get; private set; } = "";
    /// <summary>SHA-256 of phone + code, hex; the code itself is never stored.</summary>
    public string CodeHash { get; private set; } = "";
    public int Attempts { get; private set; }
    public bool Used { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }

    public static PhoneCode Issue(PhoneNumber phone, string code, DateTime nowUtc)
    {
        if (code.Length != Length || !code.All(char.IsAsciiDigit))
            throw new DomainException($"کد یک‌بارمصرف باید {Length} رقم باشد.");
        return new PhoneCode
        {
            Phone = phone.Value, CodeHash = Hash(phone.Value, code), CreatedAtUtc = nowUtc, ExpiresAtUtc = nowUtc + Lifetime,
        };
    }

    /// <summary>Checks a typed code (any digits, surrounding spaces ignored) and records the try.</summary>
    public OtpCheck Verify(string? typed, DateTime nowUtc)
    {
        if (Used) return OtpCheck.Used;
        if (Attempts >= MaxAttempts) return OtpCheck.Locked;
        if (nowUtc >= ExpiresAtUtc) return OtpCheck.Expired;

        var given = PersianText.LatinDigits((typed ?? "").Trim());
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(CodeHash), Encoding.ASCII.GetBytes(Hash(Phone, given))))
        {
            Attempts++;
            return OtpCheck.Wrong;
        }
        Used = true;
        return OtpCheck.Verified;
    }

    private static string Hash(string phone, string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(phone + ":" + code)));
}
