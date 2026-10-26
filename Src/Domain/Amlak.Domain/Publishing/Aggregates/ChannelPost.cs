using Amlak.Domain.Publishing.Enums;

namespace Amlak.Domain.Publishing.Aggregates;

/// <summary>
/// Aggregate Root پست یک آگهی در یک کانال — شناسهٔ پیام نگه داشته می‌شود تا بتوان آن را ویرایش یا حذف کرد.
/// برای هر (آگهی، کانال) حداکثر یک پست وجود دارد.
/// </summary>
public sealed class ChannelPost : AggregateRoot<long>
{
    public const int MaxErrorLength = 300;

    public long      PropertyId        { get; private set; }
    public Channel   Channel           { get; private set; }
    public string?   ExternalMessageId { get; private set; }

    /// <summary>پیام عکس‌دار است — ویرایش با editMessageCaption به‌جای editMessageText</summary>
    public bool      HasPhoto          { get; private set; }

    /// <summary>آخرین خطای ارسال؛ null یعنی همگام است</summary>
    public string?   Error             { get; private set; }

    public DateTime  CreatedAt         { get; private set; }
    public DateTime? LastSyncedAt      { get; private set; }

    /// <summary>پیامی در کانال وجود دارد</summary>
    public bool IsSent => ExternalMessageId is not null;

    private ChannelPost() { }

    private ChannelPost(long id, long propertyId, Channel channel) : base(id)
    {
        PropertyId = propertyId;
        Channel    = channel;
        CreatedAt  = DateTime.UtcNow;
    }

    public static ChannelPost Create(long id, long propertyId, Channel channel) => new(id, propertyId, channel);

    public void MarkSent(string messageId, bool hasPhoto)
    {
        ExternalMessageId = messageId;
        HasPhoto          = hasPhoto;
        Succeeded();
    }

    /// <summary>ویرایش یا حذف موفق</summary>
    public void Succeeded()
    {
        Error        = null;
        LastSyncedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// ثبت خطا بدون مسدود کردن عملیات مدیر؛ در همگام‌سازی بعدی دوباره تلاش می‌شود.
    /// متن خطا باید پیش‌تر از توکن پاک شده باشد.
    /// </summary>
    public void Failed(string error)
    {
        Error = error.Length <= MaxErrorLength ? error : error[..(MaxErrorLength - 1)] + "…";
        RaiseDomainEvent(new ChannelSyncFailedEvent(PropertyId, Channel.ToString(), Error));
    }
}
