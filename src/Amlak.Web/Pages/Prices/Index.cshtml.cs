using Amlak.Web.Read;
using Amlak.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;

namespace Amlak.Web.Pages.Prices;

/// <summary>
/// "قیمت ملک" pages in the spirit of Melkradar: asking price per m² and average rent deposit per city
/// (/قیمت-ملک) and per district of a city (/قیمت-ملک/تهران), built from published listings.
/// </summary>
[OutputCache(PolicyName = SiteCache.Listings)]
public class IndexModel(ListingQueries listings) : PageModel
{
    public const string Root = "/قیمت-ملک";

    public string? City { get; private set; }
    public List<PlacePrice> Rows { get; private set; } = [];

    public static string Path(string? city) =>
        string.Join("/", (city is null ? Root : Root + "/" + Taxonomy.Slug(city)).Split('/').Select(Uri.EscapeDataString));

    public async Task<IActionResult> OnGetAsync(string? citySlug)
    {
        if (citySlug is not null)
        {
            var slug = Taxonomy.Slug(citySlug);
            City = (await listings.CitiesAsync()).FirstOrDefault(c => Taxonomy.Slug(c.City) == slug)?.City;
            if (City is null) return NotFound();
            if (Request.Path.Value?.TrimEnd('/') != Root + "/" + slug) return RedirectPermanent(Path(City));
        }
        Rows = await listings.PlacePricesAsync(City);
        return Page();
    }
}
