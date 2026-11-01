using Amlak.Application.Leads;
using Amlak.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages.Admin;

/// <summary>Contact requests from visitors (ADM-10).</summary>
public class LeadsModel(AppDbContext db, LeadCommands leads) : PageModel
{
    public List<Lead> Items { get; private set; } = [];
    [BindProperty(SupportsGet = true)] public bool All { get; set; }

    public async Task OnGetAsync()
    {
        var q = db.Leads.AsNoTracking().Include(l => l.Property).AsQueryable();
        if (!All) q = q.Where(l => !l.Handled);
        Items = await q.OrderByDescending(l => l.CreatedAtUtc).Take(500).ToListAsync();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id, CancellationToken ct) =>
        (await leads.ToggleHandledAsync(id, ct)).Succeeded ? RedirectToPage(new { All }) : NotFound();

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken ct) =>
        (await leads.DeleteAsync(id, ct)).Succeeded ? RedirectToPage(new { All }) : NotFound();
}
