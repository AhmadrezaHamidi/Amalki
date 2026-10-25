using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Amlak.Web.Pages.Account;

public class LoginModel(SignInManager<IdentityUser> signIn) : PageModel
{
    [BindProperty, Required(ErrorMessage = "ایمیل را وارد کنید"), EmailAddress(ErrorMessage = "ایمیل معتبر نیست")]
    public string Email { get; set; } = "";

    [BindProperty, Required(ErrorMessage = "رمز عبور را وارد کنید"), DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var result = await signIn.PasswordSignInAsync(Email, Password, isPersistent: true, lockoutOnFailure: true);
        if (result.Succeeded)
            return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/Admin");

        ModelState.AddModelError("", result.IsLockedOut
            ? "حساب به‌دلیل تلاش‌های ناموفق موقتاً قفل شد. بعداً دوباره تلاش کنید."
            : "ایمیل یا رمز عبور نادرست است.");
        return Page();
    }
}
