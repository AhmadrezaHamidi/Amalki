using Amlak.Web.Read;
using Amlak.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;

namespace Amlak.Web.Pages.Properties;

[OutputCache(PolicyName = SiteCache.Listings)]
public class DetailsModel(ListingQueries listings, ConsultantQueries team) : PageModel
{
    public Property Item { get; private set; } = null!;
    public List<ListingCard> Similar { get; private set; } = [];
    public bool Sent { get; private set; }
    /// <summary>The consultant who covers this listing's city; null when no consultant is set up yet.</summary>
    public Consultant? Consultant { get; private set; }

    public async Task<IActionResult> OnGetAsync(string slug, bool sent = false)
    {
        var p = await listings.DetailAsync(slug);
        if (p is null) return NotFound();
        Item = p;
        Sent = sent;
        Similar = await listings.SimilarAsync(p, 3);
        Consultant = await team.ForCityAsync(p.City);
        return Page();
    }
}
