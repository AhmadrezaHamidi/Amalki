namespace Publishing.Application.Commands;

/// <summary>
/// همگام‌سازی پست‌های یک آگهی با وضعیتش در همهٔ کانال‌های فعال:
/// منتشرشده → ارسال یا ویرایش، غیرمنتشر → حذف. خطاها کنار هر پست ثبت می‌شوند و عملیات را نمی‌شکنند.
/// </summary>
public record SyncPropertyChannelsCommand(long PropertyId) : ICommand<bool>;

/// <summary>حذف همهٔ پست‌های آگهی‌ای که حذف شده است</summary>
public record RemovePropertyChannelsCommand(long PropertyId) : ICommand<bool>;
