namespace Amlak.Web.Domain;

public enum DealType { Sale = 1, Rent = 2 }
public enum PropertyKind { Apartment = 1, Villa = 2, Land = 3, Commercial = 4, Office = 5 }
public enum ListingStatus { Draft = 0, Published = 1, Closed = 2 }

public class Property
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public DealType Deal { get; set; }
    public PropertyKind Kind { get; set; }
    public ListingStatus Status { get; set; } = ListingStatus.Draft;

    // Sale: Price. Rent: Deposit (rahn) + MonthlyRent. Amounts in Toman.
    public long? Price { get; set; }
    public long? Deposit { get; set; }
    public long? MonthlyRent { get; set; }

    public int AreaM2 { get; set; }
    public int? Bedrooms { get; set; }
    public int? Floor { get; set; }
    public int? Parking { get; set; }
    public int? YearBuilt { get; set; }

    public string City { get; set; } = "";
    public string District { get; set; } = "";
    public string? Address { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAtUtc { get; set; }

    public List<PropertyPhoto> Photos { get; set; } = [];
    public List<ChannelPost> ChannelPosts { get; set; } = [];
}

public class PropertyPhoto
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public string Url { get; set; } = "";
    public string Alt { get; set; } = "";
    public int SortOrder { get; set; }
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
    public string? Error { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
