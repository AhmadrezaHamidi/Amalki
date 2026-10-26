using System.ComponentModel.DataAnnotations;
using Amlak.Web.Data;
using Amlak.Web.Services;
using Amlak.Web.Services.Sms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages.Track;

/// <summary>"Track a home by SMS": the visitor states what they want, confirms the number with a code, and gets texts.</summary>
public class IndexModel(AppDbContext db, OtpService otp) : PageModel
{
    public const int MaxActivePerPhone = 5;

    public class InputModel
    {
        [Required(ErrorMessage = "شمارهٔ موبایل لازم است")] public string Phone { get; set; } = "";
        [StringLength(80)] public string? Name { get; set; }
        public DealType Deal { get; set; } = DealType.Sale;
        public PropertyKind? Kind { get; set; }
        [Required(ErrorMessage = "شهر لازم است"), StringLength(80)] public string City { get; set; } = "";
        [StringLength(400)] public string? Districts { get; set; }
        /// <summary>Toman; for rent it is compared with the full-deposit equivalent.</summary>
        public string? MaxBudget { get; set; }
        [Range(0, 20)] public int? MinBedrooms { get; set; }
        public bool NotifyNew { get; set; } = true;
        public bool NotifyPriceDrop { get; set; } = true;
        public string? Code { get; set; }
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    public bool CodeSent { get; private set; }
    public bool Done { get; private set; }
    public string? Notice { get; private set; }

    public void OnGet(DealType? deal, string? city)
    {
        if (deal is { } d) Input.Deal = d;
        if (!string.IsNullOrWhiteSpace(city)) Input.City = city.Trim();
    }

    public async Task<IActionResult> OnPostSendAsync()
    {
        if (!ValidateCriteria()) return Page();
        var result = await otp.RequestAsync(Input.Phone);
        switch (result.Status)
        {
            case OtpRequestStatus.Sent:
                CodeSent = true;
                Notice = "کد تأیید پیامک شد.";
                break;
            case OtpRequestStatus.TooSoon:
                CodeSent = true;
                Notice = $"کد قبلی هنوز معتبر است؛ برای کد تازه {Fa.Digits(result.RetryAfterSeconds.ToString())} ثانیه صبر کنید.";
                break;
            case OtpRequestStatus.InvalidPhone:
                ModelState.AddModelError("Input.Phone", "شمارهٔ موبایل معتبر نیست");
                break;
            default:
                ModelState.AddModelError("", "ارسال پیامک ممکن نشد؛ کمی بعد دوباره تلاش کنید.");
                break;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmAsync()
    {
        if (!ValidateCriteria()) return Page();
        CodeSent = true;
        if (string.IsNullOrWhiteSpace(Input.Code) || !await otp.VerifyAsync(Input.Phone, Input.Code))
        {
            ModelState.AddModelError("Input.Code", "کد نادرست یا منقضی است");
            return Page();
        }

        var phone = PhoneNumber.Normalize(Input.Phone)!;
        if (await db.SavedSearches.CountAsync(s => s.Phone == phone && s.Active) >= MaxActivePerPhone)
        {
            ModelState.AddModelError("", $"برای هر شماره حداکثر {Fa.Digits(MaxActivePerPhone.ToString())} پیگیری فعال ممکن است.");
            return Page();
        }

        // A confirmed sign-up is fresh consent, so an earlier opt-out no longer applies.
        await db.SmsOptOuts.Where(x => x.Phone == phone).ExecuteDeleteAsync();
        db.SavedSearches.Add(new SavedSearch
        {
            Phone = phone, Name = Input.Name?.Trim(), Deal = Input.Deal, Kind = Input.Kind, City = Input.City.Trim(),
            Districts = string.Join("، ", AlertMatcher.SplitDistricts(Input.Districts ?? "")),
            MaxBudget = Fa.ParseLong(Input.MaxBudget), MinBedrooms = Input.MinBedrooms,
            NotifyNew = Input.NotifyNew, NotifyPriceDrop = Input.NotifyPriceDrop,
        });
        await db.SaveChangesAsync();
        Done = true;
        CodeSent = false;
        return Page();
    }

    private bool ValidateCriteria()
    {
        if (PhoneNumber.Normalize(Input.Phone) is null)
            ModelState.AddModelError("Input.Phone", "شمارهٔ موبایل معتبر نیست");
        if (!string.IsNullOrWhiteSpace(Input.MaxBudget) && Fa.ParseLong(Input.MaxBudget) is null)
            ModelState.AddModelError("Input.MaxBudget", "بودجه را فقط با عدد بنویسید");
        if (!Input.NotifyNew && !Input.NotifyPriceDrop)
            ModelState.AddModelError("", "دست‌کم یکی از پیامک‌ها را انتخاب کنید");
        return ModelState.IsValid;
    }
}
