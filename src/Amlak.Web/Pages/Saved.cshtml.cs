using Amlak.Web.Read;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Amlak.Web.Pages;

/// <summary>Saved listings (PUB-08). The ids live in the visitor's browser and arrive as ?ids=1,2,3.</summary>
public class SavedModel(ListingQueries listings) : PageModel
{
    public List<ListingCard> Items { get; private set; } = [];
    public bool HasIds { get; private set; }

    public async Task OnGetAsync(string? ids)
    {
        var list = (ids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var n) ? n : 0).Where(n => n > 0).Distinct().Take(100).ToList();
        HasIds = ids is not null;
        if (list.Count == 0) return;
        Items = await listings.ByIdsAsync(list);
    }
}
