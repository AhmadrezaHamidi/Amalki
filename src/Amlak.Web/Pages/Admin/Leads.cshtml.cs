using Amlak.Application.Leads;
using Amlak.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages.Admin;

/// <summary>Contact requests from visitors (ADM-10).</summary>
public class LeadsModel(AppDbContext db, LeadCommands leads) : PageModel
{
    public const int PageSize = 50;

    public List<LeadRow> Items { get; private set; } = [];
    [BindProperty(SupportsGet = true)] public bool All { get; set; }
    [BindProperty(SupportsGet = true)] public int P { get; set; } = 1;
    public int Total { get; private set; }
    public int Pages => (int)Math.Ceiling(Total / (double)PageSize);

    public async Task OnGetAsync()
    {
        var q = db.Leads.AsNoTracking();
        if (!All) q = q.Where(l => !l.Handled);
        Total = await q.CountAsync();
        P = Math.Clamp(P, 1, Math.Max(1, Pages));
        Items = await LeadRow.Select(q.OrderByDescending(l => l.CreatedAtUtc).ThenByDescending(l => l.Id)
            .Skip((P - 1) * PageSize).Take(PageSize)).ToListAsync();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id, CancellationToken ct) =>
        (await leads.ToggleHandledAsync(id, ct)).Succeeded ? RedirectToPage(new { All }) : NotFound();

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken ct) =>
        (await leads.DeleteAsync(id, ct)).Succeeded ? RedirectToPage(new { All }) : NotFound();
}

/// <summary>
/// What the lead lists show: only the listing's id and title, not the whole listing row
/// (description and every other column) that Include(l => l.Property) used to load for each lead.
/// </summary>
public sealed record LeadRow(int Id, string Name, string Phone, string? Message, bool Handled, DateTime CreatedAtUtc, int? PropertyId, string? PropertyTitle)
{
    public static IQueryable<LeadRow> Select(IQueryable<Lead> q) =>
        q.Select(l => new LeadRow(l.Id, l.Name, l.Phone, l.Message, l.Handled, l.CreatedAtUtc, l.PropertyId, l.Property != null ? l.Property.Title : null));
}
