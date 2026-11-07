using Amlak.Web.Data;
using Amlak.Web.Read;
using Amlak.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages;

[OutputCache(PolicyName = SiteCache.Listings)]
public class IndexModel(IDbContextFactory<AppDbContext> contexts) : PageModel
{
    public List<ListingCard> Latest { get; private set; } = [];
    public Dictionary<PropertyKind, int> CountByKind { get; private set; } = [];
    /// <summary>Busiest districts, for the "popular areas" chips.</summary>
    public List<(string City, string District, int Count)> TopAreas { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        // Three independent reads at the same time, each on its own pooled context (a DbContext runs one
        // query at a time): the page waits for the slowest instead of the sum.
        await using AppDbContext a = await contexts.CreateDbContextAsync(ct), b = await contexts.CreateDbContextAsync(ct), c = await contexts.CreateDbContextAsync(ct);
        var latest = new ListingQueries(a).LatestAsync(8, ct);
        var byKind = new ListingQueries(b).CountByKindAsync(ct: ct);
        var areas = new ListingQueries(c).TopAreasAsync(12, ct);
        await Task.WhenAll(latest, byKind, areas);
        (Latest, CountByKind, TopAreas) = (latest.Result, byKind.Result, areas.Result);
    }
}
