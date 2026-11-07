using Amlak.Web.Read;
using Amlak.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;

namespace Amlak.Web.Pages.Consultants;

/// <summary>The team page: every visible consultant with a call button and a link to ask for advice.</summary>
[OutputCache(PolicyName = SiteCache.Listings)]
public class IndexModel(ConsultantQueries team) : PageModel
{
    public List<Consultant> Team { get; private set; } = [];

    public async Task OnGetAsync() => Team = await team.ActiveAsync();
}
