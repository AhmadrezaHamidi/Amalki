using Amlak.Domain.Listing.Args;
using Amlak.Domain.Listing.Entities;
using Amlak.Domain.Listing.Enums;
using Amlak.Domain.Listing.Exceptions;

namespace Amlak.Domain.Listing.Aggregates;

/// <summary>
/// Aggregate Root آگهی ملک (فروش یا رهن و اجاره) به‌همراه عکس‌هایش.
/// چرخهٔ حیات: Draft → Published ⇄ Draft، و Published/Draft → Closed → Published (آگهی دوباره).
/// </summary>
public sealed class Property : AggregateRoot<long>
{
    public const int MaxPhotos = 20;

    private readonly List<PropertyPhoto> _photos = [];

    public string        Slug        { get; private set; } = string.Empty;
    public string        Title       { get; private set; } = string.Empty;
    public string        Description { get; private set; } = string.Empty;
    public DealType      Deal        { get; private set; }
    public PropertyKind  Kind        { get; private set; }
    public ListingStatus Status      { get; private set; }

    /// <summary>قیمت فروش (تومان) — فقط برای فروش</summary>
    public long?   Price       { get; private set; }

    /// <summary>رهن (تومان) — فقط برای اجاره</summary>
    public long?   Deposit     { get; private set; }

    /// <summary>اجارهٔ ماهانه (تومان) — فقط برای اجاره</summary>
    public long?   MonthlyRent { get; private set; }

    public int     AreaM2      { get; private set; }
    public int?    Bedrooms    { get; private set; }
    public int?    Floor       { get; private set; }
    public int?    Parking     { get; private set; }
    public int?    YearBuilt   { get; private set; }

    public string  City        { get; private set; } = string.Empty;
    public string  District    { get; private set; } = string.Empty;
    public string? Address     { get; private set; }
    public double? Latitude    { get; private set; }
    public double? Longitude   { get; private set; }

    public DateTime  CreatedAt   { get; private set; }

    /// <summary>اولین زمان انتشار — با لغو و انتشار دوباره عوض نمی‌شود</summary>
    public DateTime? PublishedAt { get; private set; }

    /// <summary>آخرین تغییر محتوا — lastmod نقشهٔ سایت</summary>
    public DateTime  UpdatedAt   { get; private set; }

    public IReadOnlyCollection<PropertyPhoto> Photos => _photos.OrderBy(p => p.SortOrder).ToList().AsReadOnly();

    public bool IsPublished => Status == ListingStatus.Published;

    private Property() { }

    private Property(CreatePropertyArg arg) : base(arg.Id)
    {
        Slug      = arg.Slug;
        Status    = ListingStatus.Draft;
        CreatedAt = DateTime.UtcNow;
        Apply(arg.Details);
    }

    /// <summary>ساخت آگهی جدید — همیشه پیش‌نویس است تا مدیر عکس‌ها را اضافه و سپس منتشر کند</summary>
    public static Property Create(CreatePropertyArg arg)
    {
        if (string.IsNullOrWhiteSpace(arg.Slug)) throw new InvalidSlugException();

        var property = new Property(arg);
        property.RaiseDomainEvent(new PropertyCreatedEvent(arg.Id, property.Title));
        return property;
    }

    /// <summary>ویرایش مشخصات. نشانی صفحه عمداً ثابت می‌ماند تا لینک‌های ایندکس‌شده نشکنند.</summary>
    public void Update(PropertyDetailsArg details)
    {
        Apply(details);
        ContentChanged();
    }

    public void Publish()
    {
        if (Status == ListingStatus.Published) throw new PropertyAlreadyPublishedException();

        Status        = ListingStatus.Published;
        PublishedAt ??= DateTime.UtcNow;
        UpdatedAt     = DateTime.UtcNow;
        RaiseDomainEvent(new PropertyPublishedEvent(Id, Slug));
    }

    /// <summary>برگشت به پیش‌نویس — از سایت و کانال‌ها برداشته می‌شود</summary>
    public void Unpublish()
    {
        if (Status != ListingStatus.Published) throw new PropertyNotPublishedException();

        Status    = ListingStatus.Draft;
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new PropertyUnpublishedEvent(Id));
    }

    /// <summary>بستن آگهی (فروخته یا اجاره رفت)</summary>
    public void Close()
    {
        if (Status == ListingStatus.Closed) throw new PropertyAlreadyClosedException();

        var wasPublished = Status == ListingStatus.Published;
        Status    = ListingStatus.Closed;
        UpdatedAt = DateTime.UtcNow;

        if (wasPublished)
            RaiseDomainEvent(new PropertyClosedEvent(Id));
    }

    /// <summary>حذف نرم آگهی؛ فایل عکس‌ها و پست کانال‌ها توسط رویداد پاک می‌شوند</summary>
    public void Delete()
    {
        IsDeleted        = true;
        ModificationTime = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new PropertyDeletedEvent(Id, _photos.Select(p => p.FileName).ToList()));
    }

    // ── عکس‌ها ────────────────────────────────────────────────────────────

    public PropertyPhoto AddPhoto(long photoId, string fileName, string? alt)
    {
        if (_photos.Count >= MaxPhotos) throw new PhotoLimitExceededException();

        var photo = new PropertyPhoto(photoId, Id, fileName, AltOrTitle(alt), _photos.Count);
        _photos.Add(photo);
        ContentChanged();
        return photo;
    }

    /// <returns>نام فایل عکس حذف‌شده تا از انبار هم پاک شود</returns>
    public string RemovePhoto(long photoId)
    {
        var photo = FindPhoto(photoId);
        _photos.Remove(photo);
        Renumber(_photos.OrderBy(p => p.SortOrder).ToList());
        ContentChanged();
        return photo.FileName;
    }

    /// <summary>چیدن دوبارهٔ عکس‌ها؛ ورودی باید دقیقاً همهٔ عکس‌های آگهی را یک‌بار داشته باشد</summary>
    public void ReorderPhotos(IReadOnlyList<long> orderedPhotoIds)
    {
        var current = _photos.Select(p => p.Id).ToHashSet();
        if (orderedPhotoIds.Count != current.Count
            || orderedPhotoIds.Distinct().Count() != orderedPhotoIds.Count
            || !orderedPhotoIds.All(current.Contains))
            throw new InvalidPhotoOrderException();

        Renumber(orderedPhotoIds.Select(FindPhoto).ToList());
        ContentChanged();
    }

    /// <summary>انتخاب عکس اصلی — به ابتدای فهرست می‌رود و بقیه ترتیب خود را حفظ می‌کنند</summary>
    public void SetCoverPhoto(long photoId)
    {
        var cover = FindPhoto(photoId);
        var rest  = _photos.Where(p => p != cover).OrderBy(p => p.SortOrder);
        Renumber([cover, .. rest]);
        ContentChanged();
    }

    public void ChangePhotoAlt(long photoId, string? alt)
    {
        FindPhoto(photoId).ChangeAlt(AltOrTitle(alt));
        UpdatedAt = DateTime.UtcNow;
    }

    // ── داخلی ─────────────────────────────────────────────────────────────

    private void Apply(PropertyDetailsArg d)
    {
        if (d.AreaM2 <= 0) throw new InvalidAreaException();
        ValidateLocation(d.Latitude, d.Longitude);
        var (price, deposit, rent) = Pricing(d.Deal, d.Price, d.Deposit, d.MonthlyRent);

        Title       = d.Title.Trim();
        Description = d.Description.Trim();
        Deal        = d.Deal;
        Kind        = d.Kind;
        Price       = price;
        Deposit     = deposit;
        MonthlyRent = rent;
        AreaM2      = d.AreaM2;
        Bedrooms    = d.Bedrooms;
        Floor       = d.Floor;
        Parking     = d.Parking;
        YearBuilt   = d.YearBuilt;
        City        = d.City.Trim();
        District    = d.District.Trim();
        Address     = string.IsNullOrWhiteSpace(d.Address) ? null : d.Address.Trim();
        Latitude    = d.Latitude;
        Longitude   = d.Longitude;
        UpdatedAt   = DateTime.UtcNow;
    }

    /// <summary>
    /// فروش فقط قیمت دارد و اجاره فقط رهن/اجاره؛ مبلغ‌های نامربوط دور ریخته می‌شوند
    /// تا آگهی فروش با رهن جاماندهٔ قبلی در جست‌وجوی اجاره نیاید.
    /// </summary>
    private static (long? Price, long? Deposit, long? Rent) Pricing(DealType deal, long? price, long? deposit, long? rent)
    {
        if (price < 0 || deposit < 0 || rent < 0) throw new InvalidAmountException();

        if (deal == DealType.Sale)
        {
            if (price is null or 0) throw new SalePriceRequiredException();
            return (price, null, null);
        }

        if (deposit is null or 0 && rent is null or 0) throw new RentAmountRequiredException();
        return (null, deposit is 0 ? null : deposit, rent is 0 ? null : rent);
    }

    private static void ValidateLocation(double? lat, double? lng)
    {
        if (lat.HasValue != lng.HasValue) throw new InvalidLocationException();
        if (lat is < -90 or > 90 || lng is < -180 or > 180) throw new InvalidLocationException();
    }

    private PropertyPhoto FindPhoto(long photoId)
        => _photos.FirstOrDefault(p => p.Id == photoId) ?? throw new PhotoNotFoundException();

    private static void Renumber(IReadOnlyList<PropertyPhoto> ordered)
    {
        for (var i = 0; i < ordered.Count; i++) ordered[i].MoveTo(i);
    }

    private string AltOrTitle(string? alt) => string.IsNullOrWhiteSpace(alt) ? Title : alt.Trim();

    /// <summary>تغییر محتوای آگهی منتشرشده باید به پست کانال‌ها هم برسد</summary>
    private void ContentChanged()
    {
        UpdatedAt = DateTime.UtcNow;
        if (Status == ListingStatus.Published)
            RaiseDomainEvent(new PropertyUpdatedEvent(Id));
    }
}
