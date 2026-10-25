using AhmadBase.Doamin;

namespace Amlak.Domain.Events.Listing;

/// <summary>آگهی جدید به‌صورت پیش‌نویس ساخته شد</summary>
public sealed record PropertyCreatedEvent(long PropertyId, string Title) : IEvent;

/// <summary>آگهی منتشر شد — باید در کانال‌ها هم قرار بگیرد</summary>
public sealed record PropertyPublishedEvent(long PropertyId, string Slug) : IEvent;

/// <summary>محتوای آگهی منتشرشده تغییر کرد — پست کانال‌ها باید ویرایش شود</summary>
public sealed record PropertyUpdatedEvent(long PropertyId) : IEvent;

/// <summary>انتشار آگهی لغو شد (برگشت به پیش‌نویس) — پست کانال‌ها باید حذف شود</summary>
public sealed record PropertyUnpublishedEvent(long PropertyId) : IEvent;

/// <summary>آگهی بسته شد (فروخته/اجاره رفت) — پست کانال‌ها باید حذف شود</summary>
public sealed record PropertyClosedEvent(long PropertyId) : IEvent;

/// <summary>آگهی حذف شد — پست کانال‌ها و فایل عکس‌ها باید پاک شوند</summary>
public sealed record PropertyDeletedEvent(long PropertyId, IReadOnlyList<string> PhotoFileNames) : IEvent;
