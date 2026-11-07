using System.ComponentModel.DataAnnotations;
using Amlak.Application.Leads;
using Amlak.Web.Data;
using Amlak.Web.Read;
using Amlak.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages;

/// <summary>Contact / visit request (PUB-06). Posted from a listing page or used on its own for a general consultation.</summary>
public class ContactModel(AppDbContext db, LeadCommands leads, ConsultantQueries team, IConfiguration cfg) : PageModel
{
    /// <summary>Route of this page; the rate limiter in Program.cs keys on it.</summary>
    public const string Path = "/تماس";

    public class InputModel
    {
        [Required(ErrorMessage = "نام را وارد کنید"), StringLength(80, ErrorMessage = "نام طولانی است")] public string Name { get; set; } = "";
        [Required(ErrorMessage = "شمارهٔ موبایل را وارد کنید")] public string Phone { get; set; } = "";
        [StringLength(1000, ErrorMessage = "پیام حداکثر ۱۰۰۰ نویسه است")] public string? Message { get; set; }
        public int? PropertyId { get; set; }
        public int? ConsultantId { get; set; }
        /// <summary>What the visitor needs (buy, sell, rent, ...); one of <see cref="Topics"/>, saved at the head of the message.</summary>
        public string? Topic { get; set; }
        // Honeypot: hidden from people, filled in by naive bots.
        public string? Website { get; set; }
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    public Property? Property { get; private set; }
    public Consultant? Consultant { get; private set; }
    public bool Sent { get; private set; }

    public static readonly string[] Topics = ["خرید ملک", "فروش ملک", "رهن و اجاره", "قیمت‌گذاری و کارشناسی", "سرمایه‌گذاری", "سایر"];

    public async Task OnGetAsync(int? propertyId, int? consultantId, string? topic, string? plan, bool sent = false)
    {
        Sent = sent;
        Input.PropertyId = propertyId;
        Input.ConsultantId = consultantId;
        Input.Topic = Topics.Contains(topic) ? topic : null;
        // From /تعرفه-ها: the chosen plan starts the message so the consultant knows what to send a payment link for.
        if (Plans.Find(cfg, plan) is { } chosen) Input.Message = $"می‌خواهم پلن «{chosen.Name}» را بگیرم.";
        Property = await FindAsync(propertyId);
        Consultant = await team.ActiveByIdAsync(consultantId);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        Property = await FindAsync(Input.PropertyId);
        Consultant = await team.ActiveByIdAsync(Input.ConsultantId, ct);
        if (!string.IsNullOrEmpty(Input.Website)) return Done(); // pretend success to the bot

        if (!PhoneNumber.TryParse(Input.Phone, out var phone) && !string.IsNullOrWhiteSpace(Input.Phone))
            ModelState.AddModelError("Input.Phone", "شمارهٔ موبایل معتبر نیست (مثل ۰۹۱۲۱۲۳۴۵۶۷)");
        if (!ModelState.IsValid) return Page();

        var topic = Topics.Contains(Input.Topic) ? Input.Topic : null;
        var message = topic is null ? Input.Message : string.IsNullOrWhiteSpace(Input.Message) ? $"موضوع: {topic}" : $"موضوع: {topic}\n{Input.Message.Trim()}";
        var sent = await leads.SubmitAsync(Input.Name, phone, message, Property?.Id, Consultant?.Id, ct);
        if (sent.Error is { } error)
        {
            ModelState.AddModelError("", error);
            return Page();
        }
        return Done();
    }

    private IActionResult Done() => Property is { } p
        // HTTP headers are ASCII-only, so both path segments are percent-encoded.
        ? Redirect($"/{Uri.EscapeDataString("ملک")}/{Uri.EscapeDataString(p.Slug)}?sent=true#contact")
        : RedirectToPage(new { sent = true, consultantId = Consultant?.Id });

    private Task<Property?> FindAsync(int? id) => id is null
        ? Task.FromResult<Property?>(null)
        : db.Properties.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.Status == ListingStatus.Published);
}
