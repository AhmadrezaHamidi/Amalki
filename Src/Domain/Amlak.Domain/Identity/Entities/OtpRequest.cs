using Amlak.Domain.Identity.Exceptions;

namespace Amlak.Domain.Identity.Entities;

/// <summary>موجودیت درخواست کد یکبار مصرف</summary>
public sealed class OtpRequest
{
    /// <summary>حداکثر تلاش اشتباه برای یک کد؛ پس از آن کد می‌سوزد تا حدس زدن ۵ رقم ممکن نباشد</summary>
    public const int MaxAttempts = 5;

    public long     Id             { get; private set; }
    public string   PhoneNumber    { get; private set; } = string.Empty;
    public string   Code           { get; private set; } = string.Empty;
    public bool     IsUsed         { get; private set; }
    public int      FailedAttempts { get; private set; }
    public DateTime CreatedAt      { get; private set; }
    public DateTime ExpiresAt      { get; private set; }

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    public bool IsValid   => !IsUsed && !IsExpired;

    private OtpRequest() { }

    public OtpRequest(long id, string phoneNumber, string code, int expiryMinutes = 5)
    {
        Id          = id;
        PhoneNumber = phoneNumber;
        Code        = code;
        CreatedAt   = DateTime.UtcNow;
        ExpiresAt   = DateTime.UtcNow.AddMinutes(expiryMinutes);
    }

    /// <summary>بررسی و مصرف کد — پس از موفقیت یا تلاش‌های زیاد، کد سوخته می‌شود</summary>
    public void Verify(string inputCode)
    {
        if (IsUsed)    throw new OtpAlreadyUsedException();
        if (IsExpired) throw new OtpExpiredException();

        if (Code != inputCode)
        {
            FailedAttempts++;
            if (FailedAttempts >= MaxAttempts) IsUsed = true;
            throw new OtpInvalidException();
        }

        IsUsed = true;
    }
}
