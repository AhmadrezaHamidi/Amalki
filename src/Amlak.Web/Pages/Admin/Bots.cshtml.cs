using Amlak.Web.Data;
using Amlak.Web.Services.Bot;
using Amlak.Web.Services.Publishing;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Amlak.Web.Pages.Admin;

/// <summary>Connect and test the Telegram / Bale bots: token check, chat-id finder, test messages.</summary>
public class BotsModel(BotDiagnostics bots, BotClients clients, AppDbContext db, IConfiguration cfg) : PageModel
{
    public Dictionary<Messenger, (int Users, int Linked)> Accounts { get; private set; } = [];
    public bool Polling(Messenger m) => clients.Polling(m);
    public bool HasSecret(Messenger m) => clients.WebhookSecret(m) is not null;
    public string WebhookUrl(Messenger m) => clients.BaseUrl + BotClients.WebhookPath(m);

    public List<BotDiagnostics.Settings> Settings { get; private set; } = [];
    public Messenger? CheckedMessenger { get; private set; }
    public BotCheck? Check { get; private set; }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostCheckAsync(Messenger messenger, CancellationToken ct)
    {
        CheckedMessenger = messenger;
        Check = await bots.CheckAsync(messenger, ct);
        await LoadAsync();
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

    /// <summary>Points the messenger at this site's webhook (Site:BaseUrl must be the public https address).</summary>
    public async Task<IActionResult> OnPostWebhookAsync(Messenger messenger, CancellationToken ct)
    {
        var secret = clients.WebhookSecret(messenger);
        if (secret is null || clients.For(messenger) is not { } bot)
        {
            TempData["Error"] = "اول توکن و WebhookSecret (حداقل ۱۶ نویسه) را تنظیم کنید.";
            return RedirectToPage();
        }
        var r = await bot.CallAsync("setWebhook", ct, ("url", WebhookUrl(messenger)), ("secret_token", secret),
            ("allowed_updates", """["message","callback_query"]"""));
        TempData[r.Ok ? "Flash" : "Error"] = r.Ok ? $"Webhook روی {WebhookUrl(messenger)} ثبت شد." : $"ثبت نشد: {r.Error}";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Settings = bots.All().ToList();
        Accounts = (await db.MessengerAccounts.AsNoTracking().GroupBy(a => a.Messenger)
                .Select(g => new { g.Key, Users = g.Count(), Linked = g.Count(a => a.Phone != null) }).ToListAsync())
            .ToDictionary(x => x.Key, x => (x.Users, x.Linked));
    }
}
