using Amlak.Web.Read;
using Amlak.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;

namespace Amlak.Web.Pages.Properties;

[OutputCache(PolicyName = SiteCache.Listings)]
public class DetailsModel(ListingQueries listings) : PageModel
{
    public Property Item { get; private set; } = null!;
    public List<ListingCard> Similar { get; private set; } = [];
    public bool Sent { get; private set; }

    public async Task<IActionResult> OnGetAsync(string slug, bool sent = false)
    {
        var p = await listings.DetailAsync(slug);
        if (p is null) return NotFound();
        Item = p;
        Sent = sent;
        Similar = await listings.SimilarAsync(p, 3);
        return Page();
    }
}
