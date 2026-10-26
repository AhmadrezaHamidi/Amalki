using System.ComponentModel.DataAnnotations.Schema;

namespace Amlak.Web.Read;

// Query side (CQRS): flat, read-only rows served straight from SQL views. Pages read these;
// writes keep going through the Property aggregate in AppDbContext. Views are defined in the
// ReadViews migration (SQL Server) and in ReadViews.Sqlite (tests).

/// <summary>One published listing, flattened with its cover photo. View <c>vw_ListingCards</c>.</summary>
public class ListingCard
{
    public int Id { get; init; }
    public string Slug { get; init; } = "";
    public string Title { get; init; } = "";
    public DealType Deal { get; init; }
    public PropertyKind Kind { get; init; }
    public string City { get; init; } = "";
    public string District { get; init; } = "";
    public int AreaM2 { get; init; }
    public int? Bedrooms { get; init; }
    public int? Floor { get; init; }
    public int? Parking { get; init; }
    public int? YearBuilt { get; init; }
    public bool? HasElevator { get; init; }
    public bool? HasStorage { get; init; }
    public bool? HasBalcony { get; init; }
    public DeedType? Deed { get; init; }
    public bool Convertible { get; init; }
    public long? Price { get; init; }
    public long? Deposit { get; init; }
    public long? MonthlyRent { get; init; }
    /// <summary>Price for sale, deposit (rahn) for rent: what visitors filter and sort by.</summary>
    public long SortPrice { get; init; }
    public DateTime? PublishedAtUtc { get; init; }
    public string? CoverUrl { get; init; }
    public string? CoverAlt { get; init; }
    public int? CoverWidth { get; init; }
    public int? CoverHeight { get; init; }
    public string? CoverVariants { get; init; }
    public int PhotoCount { get; init; }

    /// <summary>The cover as a photo, for PhotoVariants.Img (srcset of the WebP copies).</summary>
    [NotMapped]
    public PropertyPhoto? Cover => CoverUrl is null ? null
        : new() { Id = 0, PropertyId = Id, Url = CoverUrl, Alt = CoverAlt ?? "", Width = CoverWidth, Height = CoverHeight, Variants = CoverVariants };
}

/// <summary>
/// Published listing counts and totals per deal/kind/city/district, pre-aggregated.
/// On SQL Server <c>vw_ListingFacets</c> is an indexed (materialized) view kept up to date by the engine.
/// </summary>
public class ListingFacet
{
    public DealType Deal { get; init; }
    public PropertyKind Kind { get; init; }
    public string City { get; init; } = "";
    public string District { get; init; } = "";
    public long Listings { get; init; }
    public long SumPrice { get; init; }
    public long SumArea { get; init; }
}

/// <summary>Everything the admin dashboard and sidebar badges count, in one row. View <c>vw_AdminCounters</c>.</summary>
public class AdminCounters
{
    public int Published { get; init; }
    public int Drafts { get; init; }
    public int NewLeads { get; init; }
    public int OpenTickets { get; init; }
    public int AnsweredTickets { get; init; }
    public int ChannelErrors { get; init; }
}

/// <summary>Published listings in one city, with average price and area.</summary>
public record CityFacet(string City, int Listings, long AvgPrice, int AvgArea);

/// <summary>
/// Asking prices in one place (a city, or a district of a city), Melkradar-style: sale price per m² weighted by
/// area (total price / total area of sale listings with a price), and the average rent deposit.
/// </summary>
public record PlacePrice(string Name, int SaleListings, long? SalePricePerM2, int RentListings, long? AvgDeposit);
