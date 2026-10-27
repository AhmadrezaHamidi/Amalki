using Amlak.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Read;

public enum ListingSort { Newest, Cheapest, Priciest, Largest }

/// <summary>Search criteria for published listings. Null means "any".</summary>
public record ListingFilter
{
    public DealType? Deal { get; init; }
    public PropertyKind? Kind { get; init; }
    public string? City { get; init; }
    public string? District { get; init; }
    /// <summary>Several districts of the city at once; empty means any.</summary>
    public IReadOnlyCollection<string> Areas { get; init; } = [];
    /// <summary>Free text matched against title, district and city.</summary>
    public string? Text { get; init; }
    /// <summary>Bounds on price for sale and deposit (rahn) for rent.</summary>
    public long? MinPrice { get; init; }
    public long? MaxPrice { get; init; }
    public long? MaxRent { get; init; }
    public int? MinArea { get; init; }
    public int? MaxArea { get; init; }
    public int? MinBeds { get; init; }
    public int? MinYearBuilt { get; init; }
    public bool Elevator { get; init; }
    public bool Storage { get; init; }
    public bool Parking { get; init; }
    public bool HasPhoto { get; init; }
    public bool Convertible { get; init; }
    public ListingSort Sort { get; init; }
}

public record ListingPage(List<ListingCard> Items, int Total, int Page, int PageSize)
{
    public int Pages => (int)Math.Ceiling(Total / (double)PageSize);
}

/// <summary>Read side for listings. Every public listing read goes through here, never through db.Properties.</summary>
public class ListingQueries(AppDbContext db)
{
    public Task<List<ListingCard>> LatestAsync(int take, CancellationToken ct = default) =>
        db.ListingCards.OrderByDescending(c => c.PublishedAtUtc).ThenByDescending(c => c.Id).Take(take).ToListAsync(ct);

    public async Task<ListingPage> SearchAsync(ListingFilter f, int page, int pageSize, CancellationToken ct = default)
    {
        var q = Apply(db.ListingCards, f);
        var total = await q.CountAsync(ct);
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)));
        q = f.Sort switch
        {
            // Tie-breakers follow the index direction (Status, SortPrice) so the sort can read it in order.
            ListingSort.Cheapest => q.OrderBy(c => c.SortPrice).ThenBy(c => c.Id),
            ListingSort.Priciest => q.OrderByDescending(c => c.SortPrice).ThenByDescending(c => c.Id),
            ListingSort.Largest => q.OrderByDescending(c => c.AreaM2).ThenByDescending(c => c.Id),
            _ => q.OrderByDescending(c => c.PublishedAtUtc).ThenByDescending(c => c.Id),
        };
        var items = total == 0 ? [] : await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new ListingPage(items, total, page, pageSize);
    }

    public static IQueryable<ListingCard> Apply(IQueryable<ListingCard> q, ListingFilter f)
    {
        if (f.Deal is { } d) q = q.Where(c => c.Deal == d);
        if (f.Kind is { } k) q = q.Where(c => c.Kind == k);
        if (!string.IsNullOrWhiteSpace(f.City)) { var city = f.City.Trim(); q = q.Where(c => c.City == city); }
        if (!string.IsNullOrWhiteSpace(f.District)) { var district = f.District.Trim(); q = q.Where(c => c.District == district); }
        if (f.Areas.Count > 0) { var areas = f.Areas; q = q.Where(c => areas.Contains(c.District)); }
        if (!string.IsNullOrWhiteSpace(f.Text))
        {
            var text = f.Text.Trim();
            q = q.Where(c => c.Title.Contains(text) || c.District.Contains(text) || c.City.Contains(text));
        }
        if (f.MinPrice is { } minP) q = q.Where(c => c.SortPrice >= minP);
        if (f.MaxPrice is { } maxP) q = q.Where(c => c.SortPrice <= maxP);
        if (f.MaxRent is { } maxR) q = q.Where(c => (c.MonthlyRent ?? 0) <= maxR);
        if (f.MinArea is { } minA) q = q.Where(c => c.AreaM2 >= minA);
        if (f.MaxArea is { } maxA) q = q.Where(c => c.AreaM2 <= maxA);
        if (f.MinBeds is { } b and > 0) q = q.Where(c => c.Bedrooms >= b);
        if (f.MinYearBuilt is { } y) q = q.Where(c => c.YearBuilt >= y);
        if (f.Elevator) q = q.Where(c => c.HasElevator == true);
        if (f.Storage) q = q.Where(c => c.HasStorage == true);
        if (f.Parking) q = q.Where(c => c.Parking > 0);
        if (f.HasPhoto) q = q.Where(c => c.PhotoCount > 0);
        if (f.Convertible) q = q.Where(c => c.Convertible);
        return q;
    }

    /// <summary>Published listings among <paramref name="ids"/> (the visitor's saved list), newest first.</summary>
    public Task<List<ListingCard>> ByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default) =>
        db.ListingCards.Where(c => ids.Contains(c.Id)).OrderByDescending(c => c.PublishedAtUtc).ToListAsync(ct);

    /// <summary>Same deal in the same city, same kind first.</summary>
    public Task<List<ListingCard>> SimilarAsync(Property p, int take, CancellationToken ct = default) =>
        SimilarAsync(p.Id, p.Deal, p.Kind, p.City, take, ct);

    public async Task<List<ListingCard>> SimilarAsync(int id, DealType deal, PropertyKind kind, string city, int take, CancellationToken ct = default)
    {
        // Two plain queries instead of ORDER BY (Kind = @kind): that expression cannot use an index, so SQL Server
        // built the cover photo for every listing in the city before picking three (~45 ms vs ~1 ms at 20k listings).
        var near = db.ListingCards.Where(c => c.Id != id && c.Deal == deal && c.City == city);
        var items = await near.Where(c => c.Kind == kind).OrderByDescending(c => c.PublishedAtUtc).Take(take).ToListAsync(ct);
        if (items.Count < take)
            items.AddRange(await near.Where(c => c.Kind != kind).OrderByDescending(c => c.PublishedAtUtc).Take(take - items.Count).ToListAsync(ct));
        return items;
    }

    /// <summary>The full published listing with all photos, for the detail page.</summary>
    public Task<Property?> DetailAsync(string slug, CancellationToken ct = default) =>
        db.Properties.AsNoTracking().Include(x => x.Photos.OrderBy(f => f.SortOrder))
            .FirstOrDefaultAsync(x => x.Slug == slug && x.Status == ListingStatus.Published, ct);

    public async Task<Dictionary<PropertyKind, int>> CountByKindAsync(DealType? deal = null, CancellationToken ct = default)
    {
        var q = db.ListingFacets.AsQueryable();
        if (deal is { } d) q = q.Where(f => f.Deal == d);
        var rows = await q.GroupBy(f => f.Kind).Select(g => new { g.Key, N = g.Sum(x => x.Listings) }).ToListAsync(ct);
        return rows.ToDictionary(x => x.Key, x => (int)x.N);
    }

    /// <summary>Districts with the most published listings, across deals and kinds.</summary>
    public async Task<List<(string City, string District, int Count)>> TopAreasAsync(int take, CancellationToken ct = default)
    {
        var rows = await db.ListingFacets.GroupBy(f => new { f.City, f.District })
            .Select(g => new { g.Key.City, g.Key.District, N = g.Sum(x => x.Listings) })
            .OrderByDescending(x => x.N).Take(take).ToListAsync(ct);
        return rows.Select(x => (x.City, x.District, (int)x.N)).ToList();
    }

    /// <summary>Every (deal, kind, city, district) group that has published listings, with its count.</summary>
    public Task<List<ListingFacet>> PlacesAsync(CancellationToken ct = default) =>
        db.ListingFacets.ToListAsync(ct);

    /// <summary>Average asking price per m² of sale listings in a city (optionally districts and kind); null when none.</summary>
    public async Task<long?> AvgSalePricePerM2Async(string city, string? district, PropertyKind? kind,
        IReadOnlyCollection<string>? areas = null, CancellationToken ct = default)
    {
        var q = db.ListingCards.Where(c => c.Deal == DealType.Sale && c.City == city && c.Price > 0 && c.AreaM2 > 0);
        if (district is not null) q = q.Where(c => c.District == district);
        if (areas is { Count: > 0 }) q = q.Where(c => areas.Contains(c.District));
        if (kind is { } k) q = q.Where(c => c.Kind == k);
        var avg = await q.Select(c => (double?)((double)c.Price!.Value / c.AreaM2)).AverageAsync(ct);
        return avg is { } a ? (long)a : null;
    }

    /// <summary>Price table per district of <paramref name="city"/>, or per city when it is null; busiest first.</summary>
    public async Task<List<PlacePrice>> PlacePricesAsync(string? city, CancellationToken ct = default)
    {
        var cards = city is null ? db.ListingCards : db.ListingCards.Where(c => c.City == city);
        var keyed = cards.Select(c => new { Place = city == null ? c.City : c.District, c.Deal, c.Price, c.Deposit, c.AreaM2 });
        // Listings priced "توافقی" (no price) would drag the averages down, so they are left out.
        var sale = await keyed.Where(c => c.Deal == DealType.Sale && c.Price > 0 && c.AreaM2 > 0).GroupBy(c => c.Place)
            .Select(g => new { g.Key, N = g.Count(), Price = g.Sum(x => (double)x.Price!.Value), Area = g.Sum(x => (double)x.AreaM2) })
            .ToListAsync(ct);
        var rent = await keyed.Where(c => c.Deal == DealType.Rent && c.Deposit > 0).GroupBy(c => c.Place)
            .Select(g => new { g.Key, N = g.Count(), Deposit = g.Average(x => (double)x.Deposit!.Value) })
            .ToListAsync(ct);
        return sale.Select(x => x.Key).Union(rent.Select(x => x.Key))
            .Select(name =>
            {
                var s = sale.FirstOrDefault(x => x.Key == name);
                var r = rent.FirstOrDefault(x => x.Key == name);
                return new PlacePrice(name, s?.N ?? 0, s is null ? null : (long)(s.Price / s.Area), r?.N ?? 0, r is null ? null : (long)r.Deposit);
            })
            .OrderByDescending(x => x.SaleListings + x.RentListings).ThenBy(x => x.Name, StringComparer.Ordinal).ToList();
    }

    /// <summary>Cities with published listings, busiest first.</summary>
    public async Task<List<CityFacet>> CitiesAsync(DealType? deal = null, CancellationToken ct = default)
    {
        var q = db.ListingFacets.AsQueryable();
        if (deal is { } d) q = q.Where(f => f.Deal == d);
        var rows = await q.GroupBy(f => f.City)
            .Select(g => new { City = g.Key, N = g.Sum(x => x.Listings), Price = g.Sum(x => x.SumPrice), Area = g.Sum(x => x.SumArea) })
            .ToListAsync(ct);
        return rows.Where(r => r.N > 0)
            .Select(r => new CityFacet(r.City, (int)r.N, r.Price / r.N, (int)(r.Area / r.N)))
            .OrderByDescending(c => c.Listings).ThenBy(c => c.City, StringComparer.Ordinal).ToList();
    }
}

/// <summary>Read side for the admin panel.</summary>
public class AdminQueries(AppDbContext db)
{
    public async Task<AdminCounters> CountersAsync(CancellationToken ct = default) =>
        await db.AdminCounters.SingleAsync(ct);
}
