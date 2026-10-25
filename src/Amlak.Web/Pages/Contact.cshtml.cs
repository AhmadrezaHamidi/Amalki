using System.ComponentModel.DataAnnotations;
using Amlak.Web.Data;
using Amlak.Web.Services;
using Amlak.Web.Services.Publishing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages;

/// <summary>Contact / visit request (PUB-06). Posted from a listing page or used on its own for a general consultation.</summary>
public class ContactModel(AppDbContext db, LeadNotifier notifier) : PageModel
{
    /// <summary>Route of this page; the rate limiter in Program.cs keys on it.</summary>
    public const string Path = "/تماس";

    public class InputModel
    {
        [Required(ErrorMessage = "نام را وارد کنید"), StringLength(80, ErrorMessage = "نام طولانی است")] public string Name { get; set; } = "";
        [Required(ErrorMessage = "شمارهٔ موبایل را وارد کنید")] public string Phone { get; set; } = "";
        [StringLength(1000, ErrorMessage = "پیام حداکثر ۱۰۰۰ نویسه است")] public string? Message { get; set; }
        public int? PropertyId { get; set; }
        // Honeypot: hidden from people, filled in by naive bots.
        public string? Website { get; set; }
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    public Property? Property { get; private set; }
    public bool Sent { get; private set; }

    public async Task OnGetAsync(int? propertyId, bool sent = false)
    {
        Sent = sent;
        Input.PropertyId = propertyId;
        Property = await FindAsync(propertyId);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Property = await FindAsync(Input.PropertyId);
        if (!string.IsNullOrEmpty(Input.Website)) return Done(); // pretend success to the bot

        var phone = Fa.Mobile(Input.Phone);
        if (phone is null && !string.IsNullOrWhiteSpace(Input.Phone))
            ModelState.AddModelError("Input.Phone", "شمارهٔ موبایل معتبر نیست (مثل ۰۹۱۲۱۲۳۴۵۶۷)");
        if (!ModelState.IsValid) return Page();

        var lead = new Lead
        {
            Name = Input.Name.Trim(), Phone = phone!, Message = string.IsNullOrWhiteSpace(Input.Message) ? null : Input.Message.Trim(),
            PropertyId = Property?.Id,
        };
        db.Leads.Add(lead);
        await db.SaveChangesAsync();
        await notifier.NotifyAsync(lead, Property);
        return Done();
    }

    private IActionResult Done() => Property is { } p
        // HTTP headers are ASCII-only, so both path segments are percent-encoded.
        ? Redirect($"/{Uri.EscapeDataString("ملک")}/{Uri.EscapeDataString(p.Slug)}?sent=true#contact")
        : RedirectToPage(new { sent = true });

    private Task<Property?> FindAsync(int? id) => id is null
        ? Task.FromResult<Property?>(null)
        : db.Properties.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.Status == ListingStatus.Published);
}
