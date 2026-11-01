using System.ComponentModel.DataAnnotations;
using Amlak.Application.Support;
using Amlak.Web.Data;
using Amlak.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages.Support;

/// <summary>One ticket's conversation. The unguessable tracking code in the URL is the visitor's key to it.</summary>
public class TicketModel(AppDbContext db, TicketCommands tickets, TicketAccess access) : PageModel
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

    public async Task<IActionResult> OnPostAsync(string code, CancellationToken ct)
    {
        if (!access.Has(HttpContext, TicketCodes.Normalize(code))) return AskToTrack(code);
        if (ModelState.IsValid)
        {
            var sent = await tickets.ReplyFromVisitorAsync(code, Reply, ct);
            if (sent.IsNotFound) return NotFound();
            if (sent.Succeeded) return Redirect(Url(TicketCodes.Normalize(code)) + "#last");
            ModelState.AddModelError("", sent.Error!);
        }
        return await LoadAsync(code) is null ? NotFound() : Page();
    }

    // This browser has not proven the ticket is its own: send it to the tracking form (code + mobile).
    private IActionResult AskToTrack(string code) =>
        Redirect($"/{Uri.EscapeDataString("پشتیبانی")}?code={Uri.EscapeDataString(TicketCodes.Normalize(code))}#track-title");

    private async Task<Ticket?> LoadAsync(string code)
    {
        var normalized = TicketCodes.Normalize(code);
        var t = await db.Tickets.AsNoTracking().Include(x => x.Messages.OrderBy(m => m.CreatedAtUtc).ThenBy(m => m.Id))
            .FirstOrDefaultAsync(x => x.Code == normalized);
        if (t is not null) Item = t;
        return t;
    }
}
