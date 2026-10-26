using System.Text.Json;

namespace Amlak.Web.Services.Publishing;

public sealed record BotResult(bool Ok, string? MessageId, string? Error)
{
    /// <summary>Every message id of a media group (album); for a single message, just <see cref="MessageId"/>.</summary>
    public IReadOnlyList<string> MessageIds { get; init; } = MessageId is null ? [] : [MessageId];

    public static BotResult Fail(string error) => new(false, null, error);
}

/// <summary>One photo to upload: its bytes and the file name the messenger sees.</summary>
public sealed record BotPhoto(Stream Content, string FileName);

/// <summary>
/// Minimal client for the Telegram-style Bot API. Bale (tapi.bale.ai) exposes the same
/// method names and payloads, so one client serves both; only the API base differs.
/// </summary>
public class BotApiClient(HttpClient http, string apiBase, string token, string chatId)
{
    private const int MaxCaption = 1000; // Telegram caps captions at 1024 characters.
    public const int MaxAlbum = 10;      // and media groups at 10 items.

    private Uri Url(string method) => new($"{apiBase.TrimEnd('/')}/bot{token}/{method}");

    /// <summary>One inline button that opens <paramref name="url"/>; JSON for the reply_markup field.</summary>
    public static string LinkButton(string text, string url) =>
        JsonSerializer.Serialize(new { inline_keyboard = new[] { new[] { new { text, url } } } });

    public Task<BotResult> SendMessageAsync(string text, string? replyMarkup = null, CancellationToken ct = default) =>
        Post("sendMessage", Form(replyMarkup, ("chat_id", chatId), ("text", Clip(text, 4000))), ct);

    public Task<BotResult> SendPhotoAsync(BotPhoto photo, string caption, string? replyMarkup = null, CancellationToken ct = default)
    {
        var form = Form(replyMarkup, ("chat_id", chatId), ("caption", Clip(caption, MaxCaption)));
        form.Add(PhotoContent(photo), "photo", photo.FileName);
        return Post("sendPhoto", form, ct);
    }

    /// <summary>
    /// Album of 2–10 photos, caption on the first. The Bot API does not allow an inline keyboard on a
    /// media group, so the listing link travels in the caption only.
    /// </summary>
    public Task<BotResult> SendMediaGroupAsync(IReadOnlyList<BotPhoto> photos, string caption, CancellationToken ct = default)
    {
        var media = photos.Take(MaxAlbum).Select((_, i) => i == 0
            ? (object)new { type = "photo", media = $"attach://p{i}", caption = Clip(caption, MaxCaption) }
            : new { type = "photo", media = $"attach://p{i}" }).ToArray();
        var form = Form(null, ("chat_id", chatId), ("media", JsonSerializer.Serialize(media)));
        for (var i = 0; i < media.Length; i++) form.Add(PhotoContent(photos[i]), $"p{i}", photos[i].FileName);
        return Post("sendMediaGroup", form, ct);
    }

    private static StreamContent PhotoContent(BotPhoto photo)
    {
        var content = new StreamContent(photo.Content);
        content.Headers.ContentType = new(MimeFor(photo.FileName));
        return content;
    }

    // Edits resend reply_markup: Telegram drops the keyboard from an edited message that omits it.
    public Task<BotResult> EditAsync(string messageId, string text, bool isPhoto, string? replyMarkup = null, CancellationToken ct = default) =>
        isPhoto
            ? Post("editMessageCaption", Form(replyMarkup, ("chat_id", chatId), ("message_id", messageId), ("caption", Clip(text, MaxCaption))), ct)
            : Post("editMessageText", Form(replyMarkup, ("chat_id", chatId), ("message_id", messageId), ("text", Clip(text, 4000))), ct);

    public Task<BotResult> DeleteAsync(string messageId, CancellationToken ct = default) =>
        Post("deleteMessage", Form(null, ("chat_id", chatId), ("message_id", messageId)), ct);

    private static MultipartFormDataContent Form(string? replyMarkup, params (string Name, string Value)[] fields)
    {
        var form = new MultipartFormDataContent();
        foreach (var (name, value) in fields) form.Add(new StringContent(value), name);
        if (replyMarkup is not null) form.Add(new StringContent(replyMarkup), "reply_markup");
        return form;
    }

    private async Task<BotResult> Post(string method, HttpContent content, CancellationToken ct)
    {
        using (content)
        {
            try
            {
                using var resp = await http.PostAsync(Url(method), content, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);
                return Parse(body, (int)resp.StatusCode);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                // Never surface ex.Message: it can carry the request URL, which contains the bot token.
                return BotResult.Fail($"ارتباط با سرویس برقرار نشد ({ex.GetType().Name})");
            }
        }
    }

    internal BotResult Parse(string body, int status)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("ok", out var ok) && ok.GetBoolean())
            {
                var ids = new List<string>();
                if (root.TryGetProperty("result", out var r))
                {
                    // sendMediaGroup answers with an array of messages, the other methods with one.
                    var messages = r.ValueKind == JsonValueKind.Array ? r.EnumerateArray().ToList() : [r];
                    foreach (var msg in messages)
                        if (msg.ValueKind == JsonValueKind.Object && msg.TryGetProperty("message_id", out var m)) ids.Add(m.ToString());
                }
                return new BotResult(true, ids.FirstOrDefault(), null) { MessageIds = ids };
            }
            var desc = root.TryGetProperty("description", out var d) ? d.GetString() : null;
            return BotResult.Fail(Scrub(desc ?? $"HTTP {status}"));
        }
        catch (JsonException)
        {
            return BotResult.Fail($"پاسخ نامعتبر از سرویس (HTTP {status})");
        }
    }

    private string Scrub(string s) => Clip(s.Replace(token, "***"), 300);
    private static string Clip(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

    private static string MimeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg",
    };
}
