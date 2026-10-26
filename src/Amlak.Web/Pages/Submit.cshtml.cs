using System.ComponentModel.DataAnnotations;
using Amlak.Web.Data;
using Amlak.Web.Services;
using Amlak.Web.Services.Publishing;
using Amlak.Web.Services.Sms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages;

/// <summary>
/// Divar-style "post an ad": the visitor proves a mobile number with an SMS code, fills in the listing,
/// and it waits as Pending until an admin publishes it. The same page lists the visitor's own listings.
/// </summary>
public class SubmitModel(AppDbContext db, PhoneVerification verification, PhotoStorage storage, AdminNotifier notifier, TimeProvider clock) : PageModel
{
    /// <summary>Route of this page; the rate limiter in Program.cs keys on it.</summary>
    public const string Path = "/ثبت-آگهی";
    public const int MaxPhotos = 10;
    /// <summary>Open (pending) submissions one phone may have at a time.</summary>
    public const int MaxPending = 5;

    public class ListingInput
    {
        [Required(ErrorMessage = "نام را وارد کنید"), StringLength(80)] public string OwnerName { get; set; } = "";
        [Required(ErrorMessage = "عنوان لازم است"), StringLength(200, ErrorMessage = "عنوان حداکثر ۲۰۰ نویسه است")] public string Title { get; set; } = "";
        [Required(ErrorMessage = "توضیحات لازم است"), StringLength(4000, ErrorMessage = "توضیحات حداکثر ۴۰۰۰ نویسه است")] public string Description { get; set; } = "";
        public DealType Deal { get; set; } = DealType.Sale;
        public PropertyKind Kind { get; set; } = PropertyKind.Apartment;
        public string? Price { get; set; }
        public string? Deposit { get; set; }
        public string? MonthlyRent { get; set; }
        public bool Convertible { get; set; }
        [Required(ErrorMessage = "متراژ لازم است")] public string? Area { get; set; }
        [Range(0, 50)] public int? Bedrooms { get; set; }
        [Range(-5, 200)] public int? Floor { get; set; }
        [Range(0, 50)] public int? Parking { get; set; }
        [Range(1200, 1500, ErrorMessage = "سال ساخت را شمسی بنویسید، مثل ۱۴۰۰")] public int? YearBuilt { get; set; }
        public bool? HasElevator { get; set; }
        public bool? HasStorage { get; set; }
        public bool? HasBalcony { get; set; }
        public DeedType? Deed { get; set; }
        [Required(ErrorMessage = "شهر لازم است"), StringLength(80)] public string City { get; set; } = "";
        [Required(ErrorMessage = "محله لازم است"), StringLength(80)] public string District { get; set; } = "";
        // Honeypot: hidden from people, filled in by naive bots.
        public string? Website { get; set; }
    }

    [BindProperty] public ListingInput Input { get; set; } = new();
    [BindProperty] public List<IFormFile> Uploads { get; set; } = [];
    [BindProperty] public string? Phone { get; set; }
    [BindProperty] public string? Code { get; set; }

    /// <summary>The verified mobile, or null while the visitor still has to log in.</summary>
    public string? Owner { get; private set; }
    /// <summary>True between sending the code and entering it.</summary>
    public bool AwaitingCode { get; private set; }
    public bool Sent { get; private set; }
    public List<Property> Mine { get; private set; } = [];

    public async Task OnGetAsync(bool sent = false)
    {
        Sent = sent;
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
                // A code was sent a moment ago; let the visitor type it instead of sending another.
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
        if (!string.IsNullOrEmpty(Input.Website)) return RedirectToPage(new { sent = true }); // pretend success to the bot

        var price = Number(Input.Price);
        var deposit = Number(Input.Deposit);
        var rent = Number(Input.MonthlyRent);
        var area = Number(Input.Area);
        if (area is null or < 1 or > 1_000_000) ModelState.AddModelError("Input.Area", "متراژ نامعتبر است");
        if (Input.Deal == DealType.Sale && price is null or 0) ModelState.AddModelError("Input.Price", "برای فروش، قیمت لازم است");
        if (Input.Deal == DealType.Rent && deposit is null && rent is null) ModelState.AddModelError("Input.Deposit", "رهن یا اجارهٔ ماهانه را وارد کنید");
        var files = Uploads.Where(f => f.Length > 0).ToList();
        if (files.Count > MaxPhotos) ModelState.AddModelError(nameof(Uploads), $"حداکثر {Fa.Digits(MaxPhotos.ToString())} عکس");
        if (await db.Properties.CountAsync(p => p.OwnerPhone == Owner && p.Status == ListingStatus.Pending) >= MaxPending)
            ModelState.AddModelError("", "چند آگهی شما هنوز در انتظار بررسی است. پس از بررسی آن‌ها آگهی تازه بفرستید.");
        if (!ModelState.IsValid) return await RedisplayAsync();

        Property p;
        var now = clock.GetUtcNow().UtcDateTime;
        try
        {
            var details = ListingDetails.Of(Input.Title, Input.Description, Input.Kind, (int)area!.Value, Input.City, Input.District,
                Input.Bedrooms, Input.Floor, Input.Parking, Input.YearBuilt)
                with { HasElevator = Input.HasElevator, HasStorage = Input.HasStorage, HasBalcony = Input.HasBalcony, Deed = Input.Deed };
            var pricing = Pricing.For(Input.Deal, price, deposit, rent, Input.Convertible);
            p = Property.Submit(await Slugger.UniqueAsync(db, details.Title), details, pricing, Input.OwnerName, Owner, now);
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return await RedisplayAsync();
        }

        // Check every upload before storing any, so a bad second file leaves no orphaned first file.
        if (files.FirstOrDefault(f => !PhotoStorage.LooksValid(f)) is { } bad)
        {
            ModelState.AddModelError(nameof(Uploads), $"فایل «{System.IO.Path.GetFileName(bad.FileName)}» معتبر نیست (فقط JPG، PNG یا WebP تا ۵ مگابایت).");
            return await RedisplayAsync();
        }
        foreach (var file in files)
        {
            var photo = await storage.SaveAsync(file);
            if (photo is null)
            {
                foreach (var saved in p.Photos) await storage.DeleteAsync(saved);
                ModelState.AddModelError(nameof(Uploads), $"فایل «{System.IO.Path.GetFileName(file.FileName)}» معتبر نیست (فقط JPG، PNG یا WebP تا ۵ مگابایت).");
                return await RedisplayAsync();
            }
            p.AddPhoto(photo, now);
        }

        db.Properties.Add(p);
        await db.SaveChangesAsync();
        await notifier.NotifyListingAsync(p);
        return RedirectToPage(new { sent = true });
    }

    private async Task<IActionResult> RedisplayAsync()
    {
        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        Owner = verification.CurrentPhone(Request);
        if (Owner is null) return;
        Mine = await db.Properties.AsNoTracking().Where(p => p.OwnerPhone == Owner)
            .OrderByDescending(p => p.CreatedAtUtc).Take(20).ToListAsync();
    }

    private static long? Number(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var digits = new string(Fa.Latin(s).Where(char.IsAsciiDigit).ToArray());
        return digits.Length is > 0 and < 19 ? long.Parse(digits) : null;
    }
}
