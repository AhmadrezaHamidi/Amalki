using System.Text.Json;
using Amlak.Application.Contract.Services;

namespace Amlak.Persistence.EF.Services;

/// <summary>
/// کلاینت حداقلی Bot API تلگرام. بله (tapi.bale.ai) همان متدها و همان شکل داده را دارد،
/// پس یک کلاینت برای هر دو کافی است و فقط آدرس پایه فرق می‌کند.
/// </summary>
public sealed class BotApiClient(HttpClient http, string apiBase, string token, string chatId)
{
    private const int MaxCaption = 1000; // سقف کپشن تلگرام ۱۰۲۴ نویسه است
    private const int MaxText    = 4000;

    private Uri Url(string method) => new($"{apiBase.TrimEnd('/')}/bot{token}/{method}");

    public Task<ChannelResult> SendMessageAsync(string text, CancellationToken ct = default) =>
        Post("sendMessage", Form(("chat_id", chatId), ("text", Clip(text, MaxText))), ct);

    public async Task<ChannelResult> SendPhotoAsync(string filePath, string caption, CancellationToken ct = default)
    {
        var form = Form(("chat_id", chatId), ("caption", Clip(caption, MaxCaption)));
        var content = new ByteArrayContent(await File.ReadAllBytesAsync(filePath, ct));
        content.Headers.ContentType = new(Amlak.Domain.Listing.PhotoFile.ContentTypeOf(filePath));
        form.Add(content, "photo", Path.GetFileName(filePath));
        return await Post("sendPhoto", form, ct);
    }

    public Task<ChannelResult> EditAsync(string messageId, string text, bool isPhoto, CancellationToken ct = default) =>
        isPhoto
            ? Post("editMessageCaption", Form(("chat_id", chatId), ("message_id", messageId), ("caption", Clip(text, MaxCaption))), ct)
            : Post("editMessageText",    Form(("chat_id", chatId), ("message_id", messageId), ("text",    Clip(text, MaxText))), ct);

    public Task<ChannelResult> DeleteAsync(string messageId, CancellationToken ct = default) =>
        Post("deleteMessage", Form(("chat_id", chatId), ("message_id", messageId)), ct);

    private static MultipartFormDataContent Form(params (string Name, string Value)[] fields)
    {
        var form = new MultipartFormDataContent();
        foreach (var (name, value) in fields) form.Add(new StringContent(value), name);
        return form;
    }

    private async Task<ChannelResult> Post(string method, HttpContent content, CancellationToken ct)
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
                // ex.Message هرگز نمایش داده نمی‌شود: می‌تواند URL حاوی توکن ربات را داشته باشد
                return ChannelResult.Fail($"ارتباط با سرویس برقرار نشد ({ex.GetType().Name})");
            }
        }
    }

    internal ChannelResult Parse(string body, int status)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True)
            {
                string? id = null;
                if (root.TryGetProperty("result", out var r) && r.ValueKind == JsonValueKind.Object
                    && r.TryGetProperty("message_id", out var m))
                    id = m.ToString();
                return new ChannelResult(true, id, null);
            }

            var desc = root.TryGetProperty("description", out var d) ? d.GetString() : null;
            return ChannelResult.Fail(Scrub(desc ?? $"HTTP {status}"));
        }
        catch (JsonException)
        {
            return ChannelResult.Fail($"پاسخ نامعتبر از سرویس (HTTP {status})");
        }
    }

    private string Scrub(string s) => Clip(s.Replace(token, "***"), 300);

    private static string Clip(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}
