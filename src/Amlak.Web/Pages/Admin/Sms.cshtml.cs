using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Amlak.Web.Data;
using Amlak.Web.Services;
using Amlak.Web.Services.Sms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages.Admin;

public class SmsModel(AppDbContext db, BulkSmsService bulk, TimeProvider clock) : PageModel
{
    public const string DefaultTemplate = "{نام} عزیز، آگهی تازه مطابق شرایط شما:\n{عنوان} در {محله} — {قیمت}\nمشاهده: {لینک}";

    public class InputModel
    {
        [Required(ErrorMessage = "نام ارسال لازم است"), StringLength(150)] public string Name { get; set; } = "";
        [Required(ErrorMessage = "متن پیام لازم است"), StringLength(900)] public string Template { get; set; } = DefaultTemplate;
        public CampaignAudience Audience { get; set; } = CampaignAudience.Subscribers;
        [StringLength(80)] public string? City { get; set; }
        [StringLength(80)] public string? District { get; set; }
        public DealType? Deal { get; set; }
        public string? UploadedPhones { get; set; }
        public int? PropertyId { get; set; }
        /// <summary>Tehran local time from a datetime-local input; empty sends within a minute.</summary>
        public string? ScheduleLocal { get; set; }
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    public List<SmsCampaign> Campaigns { get; private set; } = [];
    public List<ListingOption> Listings { get; private set; } = [];
    public int ActiveSubscribers { get; private set; }
    public Preview? Shown { get; private set; }

    public record Preview(int Recipients, string Sample, int Segments);
    public record ListingOption(int Id, string Title, string District);

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostPreviewAsync()
    {
        if (Build() is { } c)
        {
            var recipients = await bulk.RecipientsAsync(c);
            var sample = await bulk.RenderAsync(c, recipients.FirstOrDefault().Name);
            Shown = new Preview(recipients.Count, sample, SmsText.Segments(sample));
        }
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (Build() is not { } c)
        {
            await LoadAsync();
            return Page();
        }
        db.SmsCampaigns.Add(c);
        await db.SaveChangesAsync();
        TempData["Flash"] = c.ScheduledAtUtc <= clock.GetUtcNow().UtcDateTime.AddMinutes(1)
            ? "ارسال ثبت شد و تا یک دقیقهٔ دیگر شروع می‌شود."
            : $"ارسال برای {Fa.When(c.ScheduledAtUtc)} زمان‌بندی شد.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        if (await db.SmsCampaigns.FirstOrDefaultAsync(c => c.Id == id && c.Status == CampaignStatus.Scheduled) is { } c)
        {
            c.Status = CampaignStatus.Cancelled;
            await db.SaveChangesAsync();
        }
        return RedirectToPage();
    }

    private SmsCampaign? Build()
    {
        if (Input.Audience == CampaignAudience.Uploaded && string.IsNullOrWhiteSpace(Input.UploadedPhones))
            ModelState.AddModelError("Input.UploadedPhones", "فهرست شماره‌ها را وارد کنید");

        var now = clock.GetUtcNow().UtcDateTime;
        var at = now;
        if (!string.IsNullOrWhiteSpace(Input.ScheduleLocal))
        {
            if (DateTime.TryParseExact(Input.ScheduleLocal, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
                at = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Fa.Tehran);
            else
                ModelState.AddModelError("Input.ScheduleLocal", "زمان ارسال نامعتبر است");
            if (at < now.AddMinutes(-1)) ModelState.AddModelError("Input.ScheduleLocal", "زمان ارسال گذشته است");
        }
        if (!ModelState.IsValid) return null;

        var subscribers = Input.Audience == CampaignAudience.Subscribers;
        return new SmsCampaign
        {
            Name = Input.Name.Trim(), Template = Input.Template.Trim(), Audience = Input.Audience,
            City = subscribers ? Blank(Input.City) : null, District = subscribers ? Blank(Input.District) : null,
            Deal = subscribers ? Input.Deal : null, UploadedPhones = subscribers ? null : Input.UploadedPhones,
            PropertyId = Input.PropertyId, ScheduledAtUtc = at, CreatedAtUtc = now,
        };
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : PersianText.Normalize(s);

    private async Task LoadAsync()
    {
        Campaigns = await db.SmsCampaigns.AsNoTracking().OrderByDescending(c => c.CreatedAtUtc).Take(20).ToListAsync();
        Listings = await db.Properties.AsNoTracking().Where(p => p.Status == ListingStatus.Published)
            .OrderByDescending(p => p.PublishedAtUtc).Take(100).Select(p => new ListingOption(p.Id, p.Title, p.District)).ToListAsync();
        ActiveSubscribers = await db.SavedSearches.CountAsync(s => s.Active);
    }
}
