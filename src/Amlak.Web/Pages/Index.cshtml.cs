using Amlak.Web.Read;
using Amlak.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;

namespace Amlak.Web.Pages;

[OutputCache(PolicyName = SiteCache.Listings)]
public class IndexModel(ListingQueries listings) : PageModel
{
    public List<ListingCard> Latest { get; private set; } = [];
    public Dictionary<PropertyKind, int> CountByKind { get; private set; } = [];
    /// <summary>Busiest districts, for the "popular areas" chips.</summary>
    public List<(string City, string District, int Count)> TopAreas { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Latest = await listings.LatestAsync(8);
        CountByKind = await listings.CountByKindAsync();
        TopAreas = await listings.TopAreasAsync(12);
    }
}
