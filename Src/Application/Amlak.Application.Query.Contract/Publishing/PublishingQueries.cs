namespace Publishing.Application.Queries;

/// <summary>وضعیت پست‌های یک آگهی در کانال‌ها</summary>
public record GetPropertyChannelPostsQuery(long PropertyId) : IQuery<IReadOnlyList<ChannelPostItem>>;

/// <summary>پست‌هایی که آخرین همگام‌سازی‌شان خطا داشته — فهرست کارهای مدیر</summary>
public record GetFailedChannelPostsQuery : IQuery<IReadOnlyList<ChannelPostItem>>;

public sealed record ChannelPostItem(
    long      Id,
    long      PropertyId,
    string    PropertyTitle,
    string    Channel,
    string?   ExternalMessageId,
    bool      HasPhoto,
    string?   Error,
    DateTime  CreatedAt,
    DateTime? LastSyncedAt);
