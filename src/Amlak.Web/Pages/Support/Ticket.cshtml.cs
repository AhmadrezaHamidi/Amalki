using System.ComponentModel.DataAnnotations;
using Amlak.Web.Data;
using Amlak.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages.Support;

/// <summary>One ticket's conversation. The unguessable tracking code in the URL is the visitor's key to it.</summary>
public class TicketModel(AppDbContext db, TimeProvider clock, TicketAccess access) : PageModel
{
    // HTTP headers are ASCII-only, so the Persian path segment is percent-encoded.
    public static string Url(string code, bool created = false) =>
        $"/{Uri.EscapeDataString("پشتیبانی")}/{code}" + (created ? "?created=true" : "");

    public Ticket Item { get; private set; } = null!;
    public bool Created { get; private set; }

    [BindProperty, Required(ErrorMessage = "پیام را بنویسید"), StringLength(4000, ErrorMessage = "پیام حداکثر ۴۰۰۰ نویسه است")]
    public string Reply { get; set; } = "";

    public async Task<IActionResult> OnGetAsync(string code, bool created = false)
    {
        if (!access.Has(HttpContext, TicketCodes.Normalize(code))) return AskToTrack(code);
        if (await LoadAsync(code) is null) return NotFound();
        Created = created;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string code)
    {
        if (!access.Has(HttpContext, TicketCodes.Normalize(code))) return AskToTrack(code);
        var t = await LoadAsync(code, tracking: true);
        if (t is null) return NotFound();
        if (!ModelState.IsValid) return Page();

        string body;
        try
        {
            body = t.ReplyFromVisitor(Reply, clock.GetUtcNow().UtcDateTime).Body;
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return Page();
        }
        await db.SaveChangesAsync();
        return Redirect(Url(t.Code) + "#last");
    }

    // This browser has not proven the ticket is its own: send it to the tracking form (code + mobile).
    private IActionResult AskToTrack(string code) =>
        Redirect($"/{Uri.EscapeDataString("پشتیبانی")}?code={Uri.EscapeDataString(TicketCodes.Normalize(code))}#track-title");

    private async Task<Ticket?> LoadAsync(string code, bool tracking = false)
    {
        var normalized = TicketCodes.Normalize(code);
        var q = db.Tickets.Include(x => x.Messages.OrderBy(m => m.CreatedAtUtc).ThenBy(m => m.Id)).AsQueryable();
        if (!tracking) q = q.AsNoTracking();
        var t = await q.FirstOrDefaultAsync(x => x.Code == normalized);
        if (t is not null) Item = t;
        return t;
    }
}
