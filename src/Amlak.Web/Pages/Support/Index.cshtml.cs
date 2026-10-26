using System.ComponentModel.DataAnnotations;
using Amlak.Web.Data;
using Amlak.Web.Services;
using Amlak.Web.Services.Publishing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages.Support;

/// <summary>Public support desk: open a ticket, or look one up by tracking code + mobile.</summary>
public class IndexModel(AppDbContext db, AdminNotifier notifier, TimeProvider clock) : PageModel
{
    /// <summary>Route prefix of the support pages; the rate limiter in Program.cs keys on it.</summary>
    public const string Path = "/پشتیبانی";

    public class NewInput
    {
        [Required(ErrorMessage = "نام را وارد کنید"), StringLength(80, ErrorMessage = "نام طولانی است")] public string Name { get; set; } = "";
        [Required(ErrorMessage = "شمارهٔ موبایل را وارد کنید")] public string Phone { get; set; } = "";
        [Required(ErrorMessage = "موضوع را بنویسید"), StringLength(150, ErrorMessage = "موضوع حداکثر ۱۵۰ نویسه است")] public string Subject { get; set; } = "";
        public TicketCategory Category { get; set; } = TicketCategory.General;
        [Required(ErrorMessage = "پیام را بنویسید"), StringLength(4000, ErrorMessage = "پیام حداکثر ۴۰۰۰ نویسه است")] public string Message { get; set; } = "";
        // Honeypot: hidden from people, filled in by naive bots.
        public string? Website { get; set; }
    }

    public class TrackInput
    {
        public string? Code { get; set; }
        public string? Phone { get; set; }
    }

    [BindProperty] public NewInput New { get; set; } = new();
    [BindProperty] public TrackInput Track { get; set; } = new();
    public string? TrackError { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostNewAsync()
    {
        ModelState.Clear();
        if (!TryValidateModel(New, nameof(New))) return Page();
        if (!string.IsNullOrEmpty(New.Website)) return RedirectToPage(); // pretend success to the bot
        if (!PhoneNumber.TryParse(New.Phone, out var phone))
        {
            ModelState.AddModelError("New.Phone", "شمارهٔ موبایل معتبر نیست (مثل ۰۹۱۲۱۲۳۴۵۶۷)");
            return Page();
        }

        Ticket ticket;
        try
        {
            ticket = Ticket.Open(await UniqueCodeAsync(), New.Name, phone, New.Subject, New.Category, New.Message, clock.GetUtcNow().UtcDateTime);
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return Page();
        }
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        await notifier.NotifyTicketAsync(ticket, ticket.Messages[0].Body, isNew: true);
        return Redirect(TicketModel.Url(ticket.Code, created: true));
    }

    public async Task<IActionResult> OnPostTrackAsync()
    {
        ModelState.Clear();
        var code = TicketCodes.Normalize(Track.Code);
        var phone = Fa.Mobile(Track.Phone);
        var found = code.Length > 0 && phone is not null && await db.Tickets.AnyAsync(t => t.Code == code && t.Phone == phone);
        if (found) return Redirect(TicketModel.Url(code));
        // Same message whether the code or the phone is wrong, so the form can't be used to probe codes.
        TrackError = "تیکتی با این کد پیگیری و شمارهٔ موبایل پیدا نشد.";
        return Page();
    }

    private async Task<string> UniqueCodeAsync()
    {
        string code;
        do code = TicketCodes.New(); while (await db.Tickets.AnyAsync(t => t.Code == code));
        return code;
    }
}
