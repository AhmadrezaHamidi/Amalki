using Amlak.Web.Read;

namespace Amlak.Web.Services;

/// <summary>
/// Search-as-you-type for the header box: cities and districts that have published listings, with counts.
/// Answers come from the pre-aggregated facet view and are output-cached per query, so typing costs the
/// database almost nothing. The browser side (site.js) debounces, cancels stale requests and keeps answers.
/// </summary>
public static class Suggest
{
    public const string Path = "/api/suggest";
    private const int Max = 8;

    public sealed record Item(string Label, string Hint, string Url, int Count);

    public static void MapSuggest(this WebApplication app) =>
        app.MapGet(Path, async (string? q, ListingQueries listings, CancellationToken ct) =>
        {
            var term = Normalize(q);
            if (term.Length < 2) return Results.Json(Array.Empty<Item>());
            return Results.Json(Find(await listings.PlacesAsync(ct), term));
        }).CacheOutput(b => b.Expire(TimeSpan.FromMinutes(10)).Tag(SiteCache.Listings).SetVaryByQuery("q"));

    public static List<Item> Find(IEnumerable<ListingFacet> places, string term)
    {
        var rows = places.ToList();
        var cities = rows.GroupBy(x => x.City)
            .Where(g => Normalize(g.Key).Contains(term))
            .Select(g => new Item(g.Key, "شهر", Taxonomy.EncodedPath(g.Key, null), (int)g.Sum(x => x.Listings)));
        var districts = rows.GroupBy(x => (x.City, x.District))
            .Where(g => Normalize(g.Key.District).Contains(term))
            .Select(g =>
            {
                // A district page needs a category; use the deal with the most listings there.
                var deal = g.GroupBy(x => x.Deal).OrderByDescending(d => d.Sum(x => x.Listings)).First().Key;
                return new Item(g.Key.District, g.Key.City, Taxonomy.EncodedPath(g.Key.City, Taxonomy.For(deal, null), g.Key.District), (int)g.Sum(x => x.Listings));
            });
        // Names that start with the term first, then the busiest.
        return cities.Concat(districts)
            .OrderByDescending(i => Normalize(i.Label).StartsWith(term, StringComparison.Ordinal))
            .ThenByDescending(i => i.Count)
            .Take(Max).ToList();
    }

    /// <summary>Arabic yeh/kaf typed on some keyboards match the Persian letters; spaces and half-spaces are ignored.</summary>
    public static string Normalize(string? s) =>
        (s ?? "").Trim().Replace('ي', 'ی').Replace('ك', 'ک').Replace("‌", "").Replace(" ", "");
}
