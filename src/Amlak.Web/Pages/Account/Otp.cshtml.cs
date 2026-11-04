using Amlak.Web.Services;
using Amlak.Web.Services.Sms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Amlak.Web.Pages.Account;

/// <summary>
/// Visitor login with a one-time SMS code (OTP). After login the same cookie opens "post a listing" and
/// "SMS alerts", so the code is asked for once. Admins keep the password login at /Account/Login.
/// </summary>
public class OtpModel(PhoneVerification verification) : PageModel
{
    /// <summary>Route of this page; the rate limiter in Program.cs keys on it.</summary>
    public const string Path = "/ورود";

    [BindProperty] public string? Phone { get; set; }
    [BindProperty] public string? Code { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public string? Current { get; private set; }
    public bool AwaitingCode { get; private set; }

    public void OnGet() => Current = verification.CurrentPhone(Request);

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
        // Only local paths: an open redirect would let a phishing link borrow this page.
        return LocalRedirect(Encoded(SafeReturn(ReturnUrl) ?? Path));
    }

    public IActionResult OnPostLogout()
    {
        PhoneVerification.SignOut(Response);
        return LocalRedirect("/");
    }

    /// <summary>Percent-encodes a local path for the Location header, which takes ASCII only ("/ثبت-آگهی").</summary>
    public static string Encoded(string localPath) => new Uri(new Uri("http://local"), localPath).PathAndQuery;

    /// <returns>The return path when it stays on this site, otherwise null.</returns>
    public static string? SafeReturn(string? url) =>
        !string.IsNullOrEmpty(url) && url[0] == '/' && (url.Length == 1 || (url[1] != '/' && url[1] != '\\')) ? url : null;
}
