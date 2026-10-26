namespace Amlak.Web.Domain;

public enum DealType { Sale = 1, Rent = 2 }
public enum PropertyKind { Apartment = 1, Villa = 2, Land = 3, Commercial = 4, Office = 5 }
public enum ListingStatus { Draft = 0, Published = 1, Closed = 2 }

/// <summary>
/// A listing: the aggregate root for its photos. All changes go through its methods, which keep the
/// rules (a sale has a price, a rent has a deposit or a monthly rent, only a complete listing goes live)
/// and raise the domain events that drive channel posts and caches.
/// </summary>
public class Property : AggregateRoot
{
    public const int MaxTitle = 200, MaxPlace = 80, MaxAddress = 300;

    private readonly List<PropertyPhoto> _photos = [];

    private Property() { } // EF Core

    public int Id { get; private set; }
    public string Slug { get; private set; } = "";
    public string Title { get; private set; } = "";
    public string Description { get; private set; } = "";
    public DealType Deal { get; private set; }
    public PropertyKind Kind { get; private set; }
    public ListingStatus Status { get; private set; } = ListingStatus.Draft;

    // Sale: Price. Rent: Deposit (rahn) + MonthlyRent. Amounts in Toman.
    public long? Price { get; private set; }
    public long? Deposit { get; private set; }
    public long? MonthlyRent { get; private set; }

    public int AreaM2 { get; private set; }
    public int? Bedrooms { get; private set; }
    public int? Floor { get; private set; }
    public int? Parking { get; private set; }
    public int? YearBuilt { get; private set; }

    public string City { get; private set; } = "";
    public string District { get; private set; } = "";
    public string? Address { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }

    public IReadOnlyCollection<PropertyPhoto> Photos => _photos;
    /// <summary>Messenger posts of this listing; kept in sync by the publisher, not part of the listing's rules.</summary>
    public List<ChannelPost> ChannelPosts { get; private set; } = [];

    public Pricing Pricing => Deal == DealType.Sale ? Pricing.ForSale(Price) : Pricing.ForRent(Deposit, MonthlyRent);

    public static Property Create(string slug, ListingDetails details, Pricing pricing, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(slug)) throw new DomainException("نشانی آگهی (slug) خالی است");
        var p = new Property { Slug = slug, CreatedAtUtc = nowUtc };
        p.Apply(details, pricing);
        p.Raise(new ListingCreated(p, nowUtc));
        return p;
    }

    public void Update(ListingDetails details, Pricing pricing, DateTime nowUtc)
    {
        Apply(details, pricing);
        Raise(new ListingChanged(this, nowUtc));
    }

    /// <summary>Goes live. The first publish date is kept when a listing is unpublished and published again.</summary>
    public void Publish(DateTime nowUtc)
    {
        if (Status == ListingStatus.Published) return;
        Pricing.EnsureComplete();
        Status = ListingStatus.Published;
        PublishedAtUtc ??= nowUtc;
        Raise(new ListingPublished(this, nowUtc));
    }

    /// <summary>Back to draft: hidden from the site and removed from the channels.</summary>
    public void Unpublish(DateTime nowUtc) => Leave(ListingStatus.Draft, nowUtc);

    /// <summary>Sold or rented: hidden from the site and removed from the channels.</summary>
    public void Close(DateTime nowUtc) => Leave(ListingStatus.Closed, nowUtc);

    public void ChangeStatus(ListingStatus to, DateTime nowUtc)
    {
        switch (Defined(to, "وضعیت آگهی نامعتبر است"))
        {
            case ListingStatus.Published: Publish(nowUtc); break;
            case ListingStatus.Closed: Close(nowUtc); break;
            default: Unpublish(nowUtc); break;
        }
    }

    public void TogglePublished(DateTime nowUtc)
    {
        if (Status == ListingStatus.Published) Unpublish(nowUtc);
        else Publish(nowUtc);
    }

    public PropertyPhoto AddPhoto(string url, DateTime nowUtc) => AddPhoto(PropertyPhoto.Stored(url), nowUtc);

    /// <summary>Adds a stored photo at the end; its alt text describes the listing.</summary>
    public PropertyPhoto AddPhoto(PropertyPhoto photo, DateTime nowUtc)
    {
        photo.SortOrder = _photos.Count == 0 ? 0 : _photos.Max(x => x.SortOrder) + 1;
        photo.Alt = $"{Title} - {District} {City}";
        _photos.Add(photo);
        Raise(new ListingPhotosChanged(this, nowUtc));
        return photo;
    }

    /// <summary>Moves a photo; the first one is the cover (cards, og:image, channel posts).</summary>
    /// <returns>False when the listing has no such photo.</returns>
    public bool MovePhoto(int photoId, PhotoOrder.Move move, DateTime nowUtc)
    {
        if (!PhotoOrder.Apply(_photos, photoId, move)) return false;
        Raise(new ListingPhotosChanged(this, nowUtc));
        return true;
    }

    /// <returns>The removed photo (its file is the caller's to delete), or null when the listing has no such photo.</returns>
    public PropertyPhoto? RemovePhoto(int photoId, DateTime nowUtc)
    {
        var photo = _photos.FirstOrDefault(x => x.Id == photoId);
        if (photo is null) return null;
        _photos.Remove(photo);
        Raise(new ListingPhotosChanged(this, nowUtc));
        return photo;
    }

    /// <summary>Call before removing the listing (with its photos and channel posts loaded) so they are cleaned up after.</summary>
    public void MarkDeleted(DateTime nowUtc) => Raise(new ListingDeleted(this, _photos.ToList(), ChannelPosts.Distinct().ToList(), nowUtc));

    private void Leave(ListingStatus to, DateTime nowUtc)
    {
        if (Status == to) return;
        var wasPublished = Status == ListingStatus.Published;
        Status = to;
        if (wasPublished) Raise(new ListingUnpublished(this, nowUtc));
    }

    private void Apply(ListingDetails d, Pricing pricing)
    {
        d.Validate();
        Title = d.Title; Description = d.Description; Kind = d.Kind;
        AreaM2 = d.AreaM2; Bedrooms = d.Bedrooms; Floor = d.Floor; Parking = d.Parking; YearBuilt = d.YearBuilt;
        City = d.City; District = d.District; Address = d.Address; Latitude = d.Latitude; Longitude = d.Longitude;
        Deal = pricing.Deal; Price = pricing.Price; Deposit = pricing.Deposit; MonthlyRent = pricing.MonthlyRent;
        // A published listing must stay complete.
        if (Status == ListingStatus.Published) pricing.EnsureComplete();
    }
}

/// <summary>
/// A photo of a listing. Order and alt text belong to the listing (<see cref="Property.AddPhoto(PropertyPhoto, DateTime)"/>,
/// <see cref="Property.MovePhoto"/>); size and WebP copies are facts about the stored file, recorded by the storage.
/// </summary>
public class PropertyPhoto
{
    internal PropertyPhoto() { } // EF Core and tests

    /// <summary>A stored file that is not yet part of a listing.</summary>
    public static PropertyPhoto Stored(string url) =>
        string.IsNullOrWhiteSpace(url) ? throw new DomainException("نشانی عکس خالی است") : new() { Url = url };

    public int Id { get; internal set; }
    public int PropertyId { get; private set; }
    public string Url { get; internal set; } = "";
    public string Alt { get; internal set; } = "";
    public int SortOrder { get; internal set; }
    /// <summary>Pixel size of the upright original; null for photos uploaded before WebP copies existed.</summary>
    public int? Width { get; internal set; }
    public int? Height { get; internal set; }
    /// <summary>Widths of the WebP copies next to the original (comma-separated, see PhotoVariants); null when there are none.</summary>
    public string? Variants { get; internal set; }
}

/// <summary>Everything about a listing except its price, already trimmed and normalized.</summary>
public sealed record ListingDetails
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required PropertyKind Kind { get; init; }
    public required int AreaM2 { get; init; }
    public int? Bedrooms { get; init; }
    public int? Floor { get; init; }
    public int? Parking { get; init; }
    public int? YearBuilt { get; init; }
    public required string City { get; init; }
    public required string District { get; init; }
    public string? Address { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }

    /// <summary>Trims and normalizes Arabic ي/ك so "تهران" typed either way is one city.</summary>
    public static ListingDetails Of(string title, string description, PropertyKind kind, int areaM2, string city, string district,
        int? bedrooms = null, int? floor = null, int? parking = null, int? yearBuilt = null, string? address = null,
        double? latitude = null, double? longitude = null) => new()
    {
        Title = PersianText.Normalize(title), Description = (description ?? "").Trim(), Kind = kind, AreaM2 = areaM2,
        City = PersianText.Normalize(city), District = PersianText.Normalize(district),
        Bedrooms = bedrooms, Floor = floor, Parking = parking, YearBuilt = yearBuilt,
        Address = string.IsNullOrWhiteSpace(address) ? null : PersianText.Normalize(address),
        Latitude = latitude, Longitude = longitude,
    };

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(Title) || Title.Length > Property.MaxTitle) throw new DomainException("عنوان لازم است (حداکثر ۲۰۰ نویسه)");
        if (!Enum.IsDefined(Kind)) throw new DomainException("نوع ملک نامعتبر است");
        if (AreaM2 <= 0) throw new DomainException("متراژ باید بیشتر از صفر باشد");
        if (string.IsNullOrWhiteSpace(City) || City.Length > Property.MaxPlace) throw new DomainException("شهر لازم است");
        if (string.IsNullOrWhiteSpace(District) || District.Length > Property.MaxPlace) throw new DomainException("محله لازم است");
        if (Address?.Length > Property.MaxAddress) throw new DomainException("نشانی طولانی است");
        if (Latitude is < -90 or > 90 || Longitude is < -180 or > 180) throw new DomainException("مختصات نقشه نامعتبر است");
    }
}

/// <summary>How a listing is priced. Sale: a price. Rent: a deposit (rahn), a monthly rent, or both. Toman.</summary>
public sealed record Pricing
{
    private Pricing() { }

    public DealType Deal { get; private init; }
    public long? Price { get; private init; }
    public long? Deposit { get; private init; }
    public long? MonthlyRent { get; private init; }

    public bool IsComplete => Deal == DealType.Sale ? Price > 0 : Deposit is not null || MonthlyRent is not null;

    /// <summary>A sale price; null is allowed only while the listing is a draft.</summary>
    public static Pricing ForSale(long? price) => new() { Deal = DealType.Sale, Price = NonNegative(price) };

    public static Pricing ForRent(long? deposit, long? monthlyRent) =>
        new() { Deal = DealType.Rent, Deposit = NonNegative(deposit), MonthlyRent = NonNegative(monthlyRent) };

    public static Pricing For(DealType deal, long? price, long? deposit, long? monthlyRent) => deal switch
    {
        DealType.Sale => ForSale(price),
        DealType.Rent => ForRent(deposit, monthlyRent),
        _ => throw new DomainException("نوع معامله نامعتبر است"),
    };

    /// <summary>The amount visitors budget by: the price for a sale, the deposit (or rent) for a rent.</summary>
    public long? Headline => Deal == DealType.Sale ? Price : Deposit ?? MonthlyRent;

    public void EnsureComplete()
    {
        if (IsComplete) return;
        throw new DomainException(Deal == DealType.Sale ? "برای فروش، قیمت لازم است" : "رهن یا اجارهٔ ماهانه را وارد کنید");
    }

    private static long? NonNegative(long? v) => v < 0 ? throw new DomainException("مبلغ نمی‌تواند منفی باشد") : v;
}

public enum Channel { Telegram = 1, Bale = 2 }

/// <summary>Tracks the message published to an external channel so it can be edited or removed.</summary>
public class ChannelPost
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public Channel Channel { get; set; }
    public string? ExternalMessageId { get; set; }
    /// <summary>True when the message is a photo with caption (edit uses editMessageCaption instead of editMessageText).</summary>
    public bool HasPhoto { get; set; }
    /// <summary>Ids of the other messages of an album (comma-separated); null for a single message.</summary>
    public string? ExtraMessageIds { get; set; }
    /// <summary>Fingerprint of the photos that were posted; when the listing's photos change, the post is replaced.</summary>
    public string? PhotoKey { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
