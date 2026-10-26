using Amlak.Domain.Identity.Aggregates;
using Amlak.Domain.Publishing.Enums;

namespace Amlak.Application.Contract.Services;

public interface IJwtService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
}

/// <summary>انبار فایل عکس آگهی‌ها</summary>
public interface IPhotoStorage
{
    /// <returns>نام فایل ذخیره‌شده (تصادفی)</returns>
    Task<string> SaveAsync(string extension, ReadOnlyMemory<byte> content, CancellationToken ct = default);

    void Delete(string fileName);

    /// <summary>مسیر فیزیکی فایل، یا null اگر وجود ندارد یا نام نامعتبر است</summary>
    string? PathOf(string fileName);
}

/// <summary>نتیجهٔ یک فراخوانی Bot API — خطا هرگز توکن را ندارد</summary>
public sealed record ChannelResult(bool Ok, string? MessageId, string? Error)
{
    public static ChannelResult Fail(string error) => new(false, null, error);

    /// <summary>تلگرام برای ویرایش بدون تغییر «message is not modified» می‌دهد که برای ما موفقیت است</summary>
    public bool NotModified => Error?.Contains("not modified", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>پیامی که قبلاً پاک شده، در حذف موفق حساب می‌شود</summary>
    public bool NotFound => Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true;
}

/// <summary>درگاه کانال‌های تلگرام و بله — کانالی که توکن یا شناسهٔ گفتگو ندارد غیرفعال است</summary>
public interface IChannelGateway
{
    IReadOnlyCollection<Channel> EnabledChannels { get; }

    Task<ChannelResult> SendAsync(Channel channel, string caption, string? photoPath, CancellationToken ct = default);
    Task<ChannelResult> EditAsync(Channel channel, string messageId, string caption, bool isPhoto, CancellationToken ct = default);
    Task<ChannelResult> DeleteAsync(Channel channel, string messageId, CancellationToken ct = default);
}

/// <summary>اطلاع‌رسانی به مدیران (پیامک به <c>Platform:AdminPhones</c>)</summary>
public interface IAdminNotifier
{
    Task NotifyAsync(string message, CancellationToken ct = default);
}
