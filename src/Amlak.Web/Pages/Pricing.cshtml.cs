using Amlak.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;

namespace Amlak.Web.Pages;

/// <summary>Price plans (تعرفه‌ها) for owners and agencies, plus how the commission works.</summary>
[OutputCache(PolicyName = SiteCache.Listings)]
public class PricingModel(IConfiguration cfg) : PageModel
{
    public IReadOnlyList<Plan> Plans { get; private set; } = [];

    public void OnGet() => Plans = Services.Plans.All(cfg);
}
