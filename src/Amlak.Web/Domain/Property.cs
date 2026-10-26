namespace Amlak.Web.Domain;

public enum DealType { Sale = 1, Rent = 2 }
public enum PropertyKind { Apartment = 1, Villa = 2, Land = 3, Commercial = 4, Office = 5 }
public enum ListingStatus { Draft = 0, Published = 1, Closed = 2 }
/// <summary>Ownership document, as Iranian listings label it (single-sheet, old "mangoleh" deed, ...).</summary>
public enum DeedType { Single = 1, Manguleh = 2, Promissory = 3, Endowment = 4 }

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

    // Amenities shown as "has / has not" like the big listing sites; null means not stated.
    public bool? HasElevator { get; set; }
    public bool? HasStorage { get; set; }
    public bool? HasBalcony { get; set; }
    public DeedType? Deed { get; set; }
    /// <summary>Rent only: deposit and monthly rent can be traded against each other ("qabel-e tabdil").</summary>
    public bool Convertible { get; set; }

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
    /// <summary>Pixel size of the upright original; null for photos uploaded before WebP copies existed.</summary>
    public int? Width { get; set; }
    public int? Height { get; set; }
    /// <summary>Widths of the WebP copies next to the original (comma-separated, see PhotoVariants); null when there are none.</summary>
    public string? Variants { get; set; }
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

/// <summary>A visitor's contact/visit request, optionally tied to a listing.</summary>
public class Lead
{
    public int Id { get; set; }
    public int? PropertyId { get; set; }
    public Property? Property { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Message { get; set; }
    public bool Handled { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public enum TicketCategory { General = 1, Listing = 2, Technical = 3, Complaint = 4 }
public enum TicketStatus { Open = 0, Answered = 1, Closed = 2 }

/// <summary>A support conversation. Visitors reach it by its tracking code; staff reply from the panel.</summary>
public class Ticket
{
    public int Id { get; set; }
    /// <summary>Unguessable tracking code shown to the visitor (e.g. K7Q2M9XA).</summary>
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Subject { get; set; } = "";
    public TicketCategory Category { get; set; } = TicketCategory.General;
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<TicketMessage> Messages { get; set; } = [];
}

public class TicketMessage
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public bool FromStaff { get; set; }
    public string Body { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
