using System.Text.Json;

namespace Amlak.Web.Services.Publishing;

/// <summary>One chat that recently wrote to the bot (or a channel it posted in), from <c>getUpdates</c>.</summary>
public sealed record BotChat(string Id, string Type, string Title);

/// <summary>Result of a connection check: the bot's username plus the chats seen in recent updates.</summary>
public sealed record BotCheck(bool Ok, string? Username, string? Error, IReadOnlyList<BotChat> Chats);

/// <summary>
/// Admin-panel helper for wiring a bot: confirms the token (<c>getMe</c>), lists the chats that have
/// messaged it (<c>getUpdates</c>) so the admin can copy a chat id without reading raw JSON, and sends
/// a test message. Reads only <c>{Section}:BotToken</c> / <c>ChatId</c> / <c>AdminChatId</c> from
/// configuration; the token is never shown or logged.
/// </summary>
public class BotDiagnostics(IHttpClientFactory httpFactory, IConfiguration cfg)
{
    public static readonly (Messenger Messenger, string Section, string DefaultBase, string Label)[] Messengers =
    [
        (Messenger.Telegram, "Telegram", "https://api.telegram.org", "تلگرام"),
        (Messenger.Bale, "Bale", "https://tapi.bale.ai", "بله"),
    ];

    public sealed record Settings(Messenger Messenger, string Section, string Label, bool HasToken, string? ChatId, string? AdminChatId, string MaskedToken);

    public IEnumerable<Settings> All() => Messengers.Select(m =>
    {
        var token = cfg[$"{m.Section}:BotToken"];
        return new Settings(m.Messenger, m.Section, m.Label, !string.IsNullOrWhiteSpace(token),
            Blank(cfg[$"{m.Section}:ChatId"]), Blank(cfg[$"{m.Section}:AdminChatId"]), Mask(token));
    });

    /// <summary>Shows only the bot id before the colon, e.g. <c>12345:•••</c>.</summary>
    public static string Mask(string? token) =>
        string.IsNullOrWhiteSpace(token) ? "" : (token.Split(':')[0] is { Length: > 0 } id && id != token ? id + ":•••" : "•••");

    public async Task<BotCheck> CheckAsync(Messenger messenger, CancellationToken ct = default)
    {
        if (Client(messenger, "") is not { } client) return new(false, null, "توکن ربات تنظیم نشده است", []);
        var me = await client.CallAsync("getMe", ct);
        if (!me.Ok) return new(false, null, me.Error, []);
        var username = me.Result is { } r && r.TryGetProperty("username", out var u) ? u.GetString() : null;

        // getUpdates fails with 409 while a webhook is set; that is not a token problem, so report the bot as connected.
        var updates = await client.CallAsync("getUpdates", ct);
        return new(true, username, updates.Ok ? null : updates.Error, updates.Ok ? Chats(updates.Result) : []);
    }

    /// <param name="admin">True sends to <c>AdminChatId</c>, false to the channel <c>ChatId</c>.</param>
    public async Task<BotResult> SendTestAsync(Messenger messenger, bool admin, string text, CancellationToken ct = default)
    {
        var section = Messengers.First(m => m.Messenger == messenger).Section;
        var chat = Blank(cfg[$"{section}:{(admin ? "AdminChatId" : "ChatId")}"]);
        if (chat is null) return BotResult.Fail(admin ? "AdminChatId تنظیم نشده است" : "ChatId تنظیم نشده است");
        if (Client(messenger, chat) is not { } client) return BotResult.Fail("توکن ربات تنظیم نشده است");
        return await client.SendMessageAsync(text, ct: ct);
    }

    private BotApiClient? Client(Messenger messenger, string chatId)
    {
        var m = Messengers.First(x => x.Messenger == messenger);
        var token = cfg[$"{m.Section}:BotToken"];
        if (string.IsNullOrWhiteSpace(token)) return null;
        return new BotApiClient(httpFactory.CreateClient("bots"), cfg[$"{m.Section}:ApiBase"] ?? m.DefaultBase, token.Trim(), chatId);
    }

    internal static List<BotChat> Chats(JsonElement? result)
    {
        var chats = new List<BotChat>();
        if (result is not { ValueKind: JsonValueKind.Array } updates) return chats;
        foreach (var update in updates.EnumerateArray())
        {
            if (update.ValueKind != JsonValueKind.Object) continue;
            foreach (var key in new[] { "message", "channel_post", "edited_message", "my_chat_member", "callback_query" })
            {
                if (!update.TryGetProperty(key, out var item)) continue;
                if (key == "callback_query" && item.TryGetProperty("message", out var inner)) item = inner;
                if (!item.TryGetProperty("chat", out var chat) || !chat.TryGetProperty("id", out var id)) continue;
                var title = Str(chat, "title") ?? string.Join(' ', new[] { Str(chat, "first_name"), Str(chat, "last_name") }.Where(s => s is not null));
                if (Str(chat, "username") is { } un) title = string.IsNullOrWhiteSpace(title) ? "@" + un : $"{title} (@{un})";
                var c = new BotChat(id.ToString(), Str(chat, "type") ?? "", title);
                if (chats.All(x => x.Id != c.Id)) chats.Add(c);
            }
        }
        return chats;
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
