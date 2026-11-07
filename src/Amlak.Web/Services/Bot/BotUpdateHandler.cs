using System.Text.Json;
using Amlak.Application.Support;
using Amlak.Web.Data;
using Amlak.Web.Services.Publishing;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Services.Bot;

/// <summary>
/// The two-way bot. It has no storage of its own: it reads and writes the site's database through the same
/// aggregates and use cases as the web pages, so a ticket answered in the bot is the same row the panel shows.
/// A person is identified by sharing their own contact; the number links the chat to their tickets.
/// </summary>
public sealed class BotUpdateHandler(AppDbContext db, TicketCommands tickets, BotClients clients, TimeProvider clock,
    ILogger<BotUpdateHandler> log)
{
    public const string MyTickets = "🎫 تیکت‌های من";
    public const string ShareContact = "📱 اشتراک شماره";
    private const int ListMax = 5;

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <returns>False when the update was a repeat and was skipped.</returns>
    public async Task<bool> HandleAsync(Messenger messenger, JsonElement update, CancellationToken ct = default)
    {
        if (!update.TryGetProperty("update_id", out var uid) || !uid.TryGetInt64(out var updateId)) return false;
        if (await db.MessengerInbox.AnyAsync(x => x.Messenger == messenger && x.UpdateId == updateId, ct)) return false;
        // Recorded first: a redelivery after a slow answer must not post the same ticket reply twice.
        db.MessengerInbox.Add(new MessengerInbox { Messenger = messenger, UpdateId = updateId, ReceivedAtUtc = Now });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return false; // the same update arrived twice at once
        }

        if (update.TryGetProperty("message", out var msg)) await OnMessageAsync(messenger, msg, ct);
        else if (update.TryGetProperty("callback_query", out var cb)) await OnCallbackAsync(messenger, cb, ct);
        return true;
    }

    private async Task OnMessageAsync(Messenger messenger, JsonElement msg, CancellationToken ct)
    {
        if (!msg.TryGetProperty("chat", out var chat) || Str(chat, "type") != "private") return; // groups/channels: not a conversation
        var chatId = Id(chat);
        if (chatId is null || clients.For(messenger, chatId) is not { } bot) return;
        var from = msg.TryGetProperty("from", out var f) ? f : chat;
        var account = await AccountAsync(messenger, chatId, NameOf(from), ct);

        if (msg.TryGetProperty("contact", out var contact))
        {
            await LinkAsync(account, bot, contact, Id(from), ct);
            return;
        }

        var text = (Str(msg, "text") ?? "").Trim();
        var command = text.Split(' ', '@')[0];
        if (command == "/start")
        {
            account.EndReply();
            await db.SaveChangesAsync(ct);
            await WelcomeAsync(account, bot, ct);
        }
        else if (!account.Linked)
        {
            await bot.SendMessageAsync("برای دیدن تیکت‌ها اول شماره‌تان را با دکمهٔ زیر بفرستید.", ShareKeyboard, ct);
        }
        else if (command is "/tickets" || text == MyTickets)
        {
            account.EndReply();
            await db.SaveChangesAsync(ct);
            await ListTicketsAsync(account, bot, ct);
        }
        else if (command == "/cancel")
        {
            account.EndReply();
            await db.SaveChangesAsync(ct);
            await bot.SendMessageAsync("لغو شد.", MenuKeyboard, ct);
        }
        else if (command == "/unlink")
        {
            account.Unlink();
            await db.SaveChangesAsync(ct);
            await bot.SendMessageAsync("اتصال شماره از این حساب برداشته شد.", ShareKeyboard, ct);
        }
        else if (account.ReplyingToTicket is { } code && text.Length > 0)
        {
            await ReplyAsync(account, bot, code, text, ct);
        }
        else
        {
            await bot.SendMessageAsync($"برای دیدن و پاسخ دادن به تیکت‌ها «{MyTickets}» را بزنید. تیکت تازه را در سایت ثبت کنید: {clients.BaseUrl}/پشتیبانی", MenuKeyboard, ct);
        }
    }

    private async Task OnCallbackAsync(Messenger messenger, JsonElement cb, CancellationToken ct)
    {
        var data = Str(cb, "data") ?? "";
        var callbackId = Str(cb, "id");
        if (!cb.TryGetProperty("message", out var msg) || !msg.TryGetProperty("chat", out var chat) || Str(chat, "type") != "private") return;
        var chatId = Id(chat);
        if (chatId is null || clients.For(messenger, chatId) is not { } bot) return;
        if (callbackId is not null) await bot.CallAsync("answerCallbackQuery", ct, ("callback_query_id", callbackId));

        var account = await AccountAsync(messenger, chatId, cb.TryGetProperty("from", out var from) ? NameOf(from) : null, ct);
        if (!data.StartsWith("r:")) return;
        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Code == data.Substring(2), ct);
        try
        {
            if (ticket is null) throw new DomainException("تیکت پیدا نشد.");
            account.BeginReply(ticket);
            await db.SaveChangesAsync(ct);
            await bot.SendMessageAsync($"پاسخ خود به تیکت {TicketCodes.Pretty(ticket.Code)} («{ticket.Subject}») را بنویسید. برای انصراف /cancel", ct: ct);
        }
        catch (DomainException ex)
        {
            await bot.SendMessageAsync(ex.Message, MenuKeyboard, ct);
        }
    }

    private async Task LinkAsync(MessengerAccount account, BotApiClient bot, JsonElement contact, string? senderId, CancellationToken ct)
    {
        try
        {
            // Telegram/Bale put the contact owner's user id on a contact shared with request_contact; a forwarded
            // contact of someone else carries their id (or none), which must not unlock their tickets.
            var own = senderId is not null && Id(contact, "user_id") == senderId;
            account.Link(PhoneNumber.Parse(Str(contact, "phone_number")), own, Now);
            await db.SaveChangesAsync(ct);
            var count = await db.Tickets.CountAsync(t => t.Phone == account.Phone, ct);
            await bot.SendMessageAsync($"✅ حساب شما به شمارهٔ {account.Phone} وصل شد. {Fa.Digits(count.ToString())} تیکت با این شماره ثبت شده است.\nپاسخ پشتیبانی از این به بعد همین‌جا هم می‌رسد.", MenuKeyboard, ct);
        }
        catch (DomainException ex)
        {
            await bot.SendMessageAsync(ex.Message, ShareKeyboard, ct);
        }
    }

    private async Task WelcomeAsync(MessengerAccount account, BotApiClient bot, CancellationToken ct)
    {
        var site = account.Name.Length > 0 ? $"سلام {account.Name}! " : "سلام! ";
        if (account.Linked)
            await bot.SendMessageAsync(site + $"حساب شما به شمارهٔ {account.Phone} وصل است.", MenuKeyboard, ct);
        else
            await bot.SendMessageAsync(site + "برای دیدن تیکت‌های پشتیبانی و گرفتن پاسخ‌ها در همین‌جا، شمارهٔ موبایلی را که در سایت استفاده می‌کنید با دکمهٔ زیر بفرستید.", ShareKeyboard, ct);
    }

    private async Task ListTicketsAsync(MessengerAccount account, BotApiClient bot, CancellationToken ct)
    {
        var list = await db.Tickets.AsNoTracking().Where(t => t.Phone == account.Phone)
            .OrderByDescending(t => t.UpdatedAtUtc).Take(ListMax)
            .Select(t => new { t.Code, t.Subject, t.Status, Last = t.Messages.OrderByDescending(m => m.Id).Select(m => new { m.FromStaff, m.Body }).FirstOrDefault() })
            .ToListAsync(ct);
        if (list.Count == 0)
        {
            await bot.SendMessageAsync($"تیکتی با شمارهٔ {account.Phone} پیدا نشد. برای ثبت تیکت: {clients.BaseUrl}/پشتیبانی", MenuKeyboard, ct);
            return;
        }
        var lines = list.Select(t =>
            $"🎫 {TicketCodes.Pretty(t.Code)} · {Labels.Status(t.Status)}\n{t.Subject}" +
            (t.Last is null ? "" : $"\n{(t.Last.FromStaff ? "پشتیبانی" : "شما")}: {Snippet(t.Last.Body)}"));
        var buttons = list.Where(t => t.Status != TicketStatus.Closed)
            .Select(t => new[] { new { text = $"✍️ پاسخ به {TicketCodes.Pretty(t.Code)}", callback_data = "r:" + t.Code } }).ToArray();
        var markup = buttons.Length == 0 ? null : JsonSerializer.Serialize(new { inline_keyboard = buttons });
        await bot.SendMessageAsync(string.Join("\n\n", lines), markup, ct);
    }

    private async Task ReplyAsync(MessengerAccount account, BotApiClient bot, string code, string text, CancellationToken ct)
    {
        var ticket = await db.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Code == code, ct);
        if (ticket is null || ticket.Phone != account.Phone)
        {
            account.EndReply();
            await db.SaveChangesAsync(ct);
            await bot.SendMessageAsync("این تیکت به شمارهٔ شما تعلق ندارد.", MenuKeyboard, ct);
            return;
        }
        account.EndReply();
        var r = await tickets.ReplyFromVisitorAsync(code, text, ct); // saves the account change too (same DbContext)
        if (!r.Succeeded) await db.SaveChangesAsync(ct);
        await bot.SendMessageAsync(r.Succeeded ? $"✅ پیام شما در تیکت {TicketCodes.Pretty(code)} ثبت شد." : r.Error ?? "ثبت نشد.", MenuKeyboard, ct);
    }

    private async Task<MessengerAccount> AccountAsync(Messenger messenger, string chatId, string? name, CancellationToken ct)
    {
        var account = await db.MessengerAccounts.FirstOrDefaultAsync(a => a.Messenger == messenger && a.ChatId == chatId, ct);
        if (account is null)
        {
            account = MessengerAccount.Start(messenger, chatId, name, Now);
            db.MessengerAccounts.Add(account);
        }
        else account.Seen(name, Now);
        await db.SaveChangesAsync(ct);
        return account;
    }

    public static readonly string ShareKeyboard = JsonSerializer.Serialize(new
    {
        keyboard = new[] { new[] { new { text = ShareContact, request_contact = true } } }, resize_keyboard = true, one_time_keyboard = true,
    });

    public static readonly string MenuKeyboard = JsonSerializer.Serialize(new
    {
        keyboard = new[] { new[] { new { text = MyTickets } } }, resize_keyboard = true,
    });

    private static string Snippet(string s) => s.Length <= 200 ? s : s[..199] + "…";

    private static string? NameOf(JsonElement from) =>
        string.Join(' ', new[] { Str(from, "first_name"), Str(from, "last_name") }.Where(x => !string.IsNullOrWhiteSpace(x)));

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string? Id(JsonElement e, string name = "id") =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetRawText() : null;
}
