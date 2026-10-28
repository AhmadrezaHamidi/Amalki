using System.ComponentModel.DataAnnotations;
using Amlak.Web.Data;
using Amlak.Web.Services;
using Amlak.Web.Services.Sms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages;

/// <summary>
/// "Track a home by SMS": the visitor proves a mobile number with a code (the same login as /ثبت-آگهی),
/// says what they want, and new matching listings and price drops are texted to them.
/// </summary>
public class TrackModel(AppDbContext db, PhoneVerification verification, SmsGateway sms, TimeProvider clock) : PageModel
{
    /// <summary>Route of this page; the rate limiter in Program.cs keys on it.</summary>
    public const string Path = "/پیگیری";
    public const int MaxActivePerPhone = 5;

    public class SearchInput
    {
        [StringLength(80)] public string? Name { get; set; }
        public DealType Deal { get; set; } = DealType.Sale;
        public PropertyKind? Kind { get; set; }
        [Required(ErrorMessage = "شهر لازم است"), StringLength(80)] public string City { get; set; } = "";
        [StringLength(400)] public string? Districts { get; set; }
        /// <summary>Toman; a rent is compared by its full-deposit equivalent.</summary>
        public string? MaxBudget { get; set; }
        [Range(0, 20)] public int? MinBedrooms { get; set; }
        public bool NotifyNew { get; set; } = true;
        public bool NotifyPriceDrop { get; set; } = true;
    }

    [BindProperty] public SearchInput Input { get; set; } = new();
    [BindProperty] public string? Phone { get; set; }
    [BindProperty] public string? Code { get; set; }

    /// <summary>The verified mobile, or null while the visitor still has to log in.</summary>
    public string? Owner { get; private set; }
    public bool AwaitingCode { get; private set; }
    public bool Saved { get; private set; }
    public List<SavedSearch> Mine { get; private set; } = [];

    public async Task OnGetAsync(DealType? deal, string? city, bool saved = false)
    {
        if (deal is { } d && Enum.IsDefined(d)) Input.Deal = d;
        if (!string.IsNullOrWhiteSpace(city)) Input.City = city.Trim();
        Saved = saved;
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostSendCodeAsync()
    {
        var mobile = Fa.Mobile(Phone);
        if (mobile is null)
        {
            ModelState.AddModelError(nameof(Phone), "شمارهٔ موبایل معتبر نیست (مثل ۰۹۱۲۱۲۳۴۵۶۷)");
            return Page();
        }
        Phone = mobile;
        switch (await verification.SendAsync(mobile))
        {
            case SendResult.Failed:
                ModelState.AddModelError(nameof(Phone), "ارسال پیامک ممکن نشد. چند دقیقهٔ دیگر دوباره تلاش کنید.");
                return Page();
            case SendResult.TooSoon:
                ModelState.AddModelError(nameof(Code), "کد قبلی هنوز معتبر است؛ دو دقیقه بعد می‌توانید کد تازه بگیرید.");
                break;
        }
        AwaitingCode = true;
        return Page();
    }

    public async Task<IActionResult> OnPostVerifyAsync()
    {
        var mobile = Fa.Mobile(Phone);
        if (mobile is null) return RedirectToPage();
        if (!await verification.VerifyAsync(mobile, Code ?? ""))
        {
            Phone = mobile;
            AwaitingCode = true;
            ModelState.AddModelError(nameof(Code), "کد درست نیست یا منقضی شده است.");
            return Page();
        }
        verification.SignIn(Response, mobile);
        return RedirectToPage();
    }

    public IActionResult OnPostLogout()
    {
        PhoneVerification.SignOut(Response);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Owner = verification.CurrentPhone(Request);
        if (Owner is null) return RedirectToPage();

        var budget = Fa.Amount(Input.MaxBudget);
        if (!string.IsNullOrWhiteSpace(Input.MaxBudget) && budget is null) ModelState.AddModelError("Input.MaxBudget", "بودجه را فقط با عدد بنویسید");
        if (!Input.NotifyNew && !Input.NotifyPriceDrop) ModelState.AddModelError("", "دست‌کم یکی از پیامک‌ها را انتخاب کنید");
        if (!Enum.IsDefined(Input.Deal) || Input.Kind is { } k && !Enum.IsDefined(k)) ModelState.AddModelError("", "نوع معامله یا ملک نامعتبر است");
        if (await db.SavedSearches.CountAsync(s => s.Phone == Owner && s.Active) >= MaxActivePerPhone)
            ModelState.AddModelError("", $"برای هر شماره حداکثر {Fa.Digits(MaxActivePerPhone.ToString())} پیگیری فعال ممکن است. یکی را لغو کنید.");
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        await sms.OptInAsync(Owner);
        db.SavedSearches.Add(new SavedSearch
        {
            Phone = Owner, Name = string.IsNullOrWhiteSpace(Input.Name) ? null : Input.Name.Trim(),
            Deal = Input.Deal, Kind = Input.Kind, City = PersianText.Normalize(Input.City),
            Districts = string.Join("، ", AlertMatcher.Districts(Input.Districts)),
            MaxBudget = budget, MinBedrooms = Input.MinBedrooms,
            NotifyNew = Input.NotifyNew, NotifyPriceDrop = Input.NotifyPriceDrop,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        });
        await db.SaveChangesAsync();
        return RedirectToPage(new { saved = true });
    }

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        Owner = verification.CurrentPhone(Request);
        if (Owner is null) return RedirectToPage();
        if (await db.SavedSearches.FirstOrDefaultAsync(s => s.Id == id && s.Phone == Owner) is { } s)
        {
            s.Active = false;
            await db.SaveChangesAsync();
        }
        return RedirectToPage();
    }

    /// <summary>Ends every tracking of this number and blocks campaigns to it, like replying «لغو۱۱».</summary>
    public async Task<IActionResult> OnPostStopAllAsync()
    {
        Owner = verification.CurrentPhone(Request);
        if (Owner is not null) await sms.OptOutAsync(Owner);
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Owner = verification.CurrentPhone(Request);
        if (Owner is null) return;
        Mine = await db.SavedSearches.AsNoTracking().Where(s => s.Phone == Owner && s.Active)
            .OrderByDescending(s => s.CreatedAtUtc).ToListAsync();
    }
}
