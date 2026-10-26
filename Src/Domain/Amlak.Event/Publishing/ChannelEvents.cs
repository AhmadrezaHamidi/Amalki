using AhmadBase.Doamin;

namespace Amlak.Domain.Events.Publishing;

/// <summary>ارسال/ویرایش/حذف پست کانال ناموفق بود</summary>
public sealed record ChannelSyncFailedEvent(long PropertyId, string Channel, string Error) : IEvent;
