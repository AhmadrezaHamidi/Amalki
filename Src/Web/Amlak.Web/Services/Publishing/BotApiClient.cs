using System.Text.Json;

namespace Amlak.Web.Services.Publishing;

public sealed record BotResult(bool Ok, string? MessageId, string? Error)
{
    public static BotResult Fail(string error) => new(false, null, error);
}

/// <summary>
/// Minimal client for the Telegram-style Bot API. Bale (tapi.bale.ai) exposes the same
/// method names and payloads, so one client serves both; only the API base differs.
/// </summary>
public class BotApiClient(HttpClient http, string apiBase, string token, string chatId)
{
    private const int MaxCaption = 1000; // Telegram caps captions at 1024 characters.

    private Uri Url(string method) => new($"{apiBase.TrimEnd('/')}/bot{token}/{method}");

    public Task<BotResult> SendMessageAsync(string text, CancellationToken ct = default) =>
        Post("sendMessage", Form(("chat_id", chatId), ("text", Clip(text, 4000))), ct);

    public async Task<BotResult> SendPhotoAsync(string filePath, string caption, CancellationToken ct = default)
    {
        using var form = Form(("chat_id", chatId), ("caption", Clip(caption, MaxCaption)));
        await using var file = File.OpenRead(filePath);
        var content = new StreamContent(file);
        content.Headers.ContentType = new(MimeFor(filePath));
        form.Add(content, "photo", Path.GetFileName(filePath));
        return await Post("sendPhoto", form, ct);
    }

    public Task<BotResult> EditAsync(string messageId, string text, bool isPhoto, CancellationToken ct = default) =>
        isPhoto
            ? Post("editMessageCaption", Form(("chat_id", chatId), ("message_id", messageId), ("caption", Clip(text, MaxCaption))), ct)
            : Post("editMessageText", Form(("chat_id", chatId), ("message_id", messageId), ("text", Clip(text, 4000))), ct);

    public Task<BotResult> DeleteAsync(string messageId, CancellationToken ct = default) =>
        Post("deleteMessage", Form(("chat_id", chatId), ("message_id", messageId)), ct);

    private static MultipartFormDataContent Form(params (string Name, string Value)[] fields)
    {
        var form = new MultipartFormDataContent();
        foreach (var (name, value) in fields) form.Add(new StringContent(value), name);
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
                string? id = null;
                if (root.TryGetProperty("result", out var r) && r.ValueKind == JsonValueKind.Object
                    && r.TryGetProperty("message_id", out var m))
                    id = m.ToString();
                return new BotResult(true, id, null);
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
