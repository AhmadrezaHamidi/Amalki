using Amlak.Web.Services.Publishing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Amlak.Web.Pages.Admin;

/// <summary>Connect and test the Telegram / Bale bots: token check, chat-id finder, test messages.</summary>
public class BotsModel(BotDiagnostics bots, IConfiguration cfg) : PageModel
{
    public List<BotDiagnostics.Settings> Settings { get; private set; } = [];
    public Messenger? CheckedMessenger { get; private set; }
    public BotCheck? Check { get; private set; }

    public void OnGet() => Load();

    public async Task<IActionResult> OnPostCheckAsync(Messenger messenger, CancellationToken ct)
    {
        CheckedMessenger = messenger;
        Check = await bots.CheckAsync(messenger, ct);
        Load();
        return Page();
    }

    public async Task<IActionResult> OnPostTestAsync(Messenger messenger, bool admin, CancellationToken ct)
    {
        var site = cfg["Site:Name"] ?? "Royal Fine Homes";
        var text = $"✅ پیام آزمایشی از پنل {site}\nاتصال ربات به {(admin ? "چت مدیر" : "کانال")} برقرار است.";
        var r = await bots.SendTestAsync(messenger, admin, text, ct);
        TempData[r.Ok ? "Flash" : "Error"] = r.Ok ? "پیام آزمایشی فرستاده شد." : $"ارسال نشد: {r.Error}";
        return RedirectToPage();
    }

    private void Load() => Settings = bots.All().ToList();
}
