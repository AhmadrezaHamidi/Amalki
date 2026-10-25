using Amlak.Domain.Listing.Enums;

namespace Listing.Application.Queries;

// ── سایت عمومی (فقط آگهی‌های منتشرشده) ─────────────────────────────────

/// <summary>
/// جست‌وجوی آگهی‌های منتشرشده با فیلتر.
/// بازهٔ قیمت برای فروش روی قیمت کل و برای اجاره روی مبلغ رهن اعمال می‌شود.
/// </summary>
public record SearchPropertiesQuery(
    int           Page        = 1,
    int           PageSize    = 12,
    DealType?     Deal        = null,
    PropertyKind? Kind        = null,
    string?       City        = null,
    string?       District    = null,
    long?         MinPrice    = null,
    long?         MaxPrice    = null,
    long?         MaxRent     = null,
    int?          MinArea     = null,
    int?          MaxArea     = null,
    int?          MinBedrooms = null,
    ListingSort   Sort        = ListingSort.Newest) : IQuery<PagedResult<PropertyCard>>;

/// <summary>جزئیات آگهی منتشرشده با نشانی صفحه — پیش‌نویس و بسته‌شده null می‌دهد (۴۰۴)</summary>
public record GetPropertyBySlugQuery(string Slug) : IQuery<PropertyDetail?>;

/// <summary>آگهی‌های مشابه منتشرشده (همان شهر و نوع معامله)</summary>
public record GetSimilarPropertiesQuery(long PropertyId, int Take = 4) : IQuery<IReadOnlyList<PropertyCard>>;

/// <summary>شهرها و محله‌های دارای آگهی منتشرشده — برای فیلتر و صفحه‌های سئوی محلی</summary>
public record GetLocationsQuery : IQuery<IReadOnlyList<LocationItem>>;

/// <summary>داده‌های نقشهٔ سایت (sitemap.xml)</summary>
public record GetSitemapEntriesQuery : IQuery<IReadOnlyList<SitemapEntry>>;

// ── پنل مدیریت (همهٔ وضعیت‌ها) ──────────────────────────────────────────

public record GetAdminPropertiesQuery(
    int            Page     = 1,
    int            PageSize = 20,
    ListingStatus? Status   = null,
    string?        Search   = null) : IQuery<PagedResult<PropertyAdminListItem>>;

public record GetPropertyQuery(long Id) : IQuery<PropertyDetail?>;

// ── نتیجه‌ها ────────────────────────────────────────────────────────────

public sealed record PropertyCard(
    long      Id,
    string    Slug,
    string    Title,
    string    Deal,
    string    Kind,
    long?     Price,
    long?     Deposit,
    long?     MonthlyRent,
    int       AreaM2,
    int?      Bedrooms,
    string    City,
    string    District,
    string?   CoverPhotoUrl,
    DateTime? PublishedAt);

public sealed record PropertyDetail(
    long      Id,
    string    Slug,
    string    Title,
    string    Description,
    string    Deal,
    string    Kind,
    string    Status,
    long?     Price,
    long?     Deposit,
    long?     MonthlyRent,
    int       AreaM2,
    int?      Bedrooms,
    int?      Floor,
    int?      Parking,
    int?      YearBuilt,
    string    City,
    string    District,
    string?   Address,
    double?   Latitude,
    double?   Longitude,
    DateTime  CreatedAt,
    DateTime? PublishedAt,
    DateTime  UpdatedAt,
    IReadOnlyList<PhotoItem> Photos);

public sealed record PhotoItem(long Id, string Url, string Alt, int SortOrder);

public sealed record PropertyAdminListItem(
    long      Id,
    string    Slug,
    string    Title,
    string    Deal,
    string    Status,
    string    City,
    string    District,
    int       PhotoCount,
    int       NewLeadCount,
    bool      HasChannelError,
    DateTime  CreatedAt,
    DateTime? PublishedAt);

public sealed record LocationItem(string City, string District, int Count);

public sealed record SitemapEntry(string Slug, string Title, DateTime LastModified, string? CoverPhotoUrl);
