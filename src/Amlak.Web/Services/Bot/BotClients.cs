using Amlak.Web.Services.Publishing;

namespace Amlak.Web.Services.Bot;

/// <summary>Builds Bot API clients for private chats from the same <c>{Section}:BotToken</c> the channel uses.</summary>
public class BotClients(IHttpClientFactory httpFactory, IConfiguration cfg)
{
    public static string Section(Messenger m) => m == Messenger.Bale ? "Bale" : "Telegram";
    private static string DefaultBase(Messenger m) => m == Messenger.Bale ? "https://tapi.bale.ai" : "https://api.telegram.org";

    public bool Configured(Messenger m) => !string.IsNullOrWhiteSpace(cfg[$"{Section(m)}:BotToken"]);

    public IEnumerable<Messenger> All => Enum.GetValues<Messenger>().Where(Configured);

    public BotApiClient? For(Messenger m, string chatId = "")
    {
        var token = cfg[$"{Section(m)}:BotToken"];
        if (string.IsNullOrWhiteSpace(token)) return null;
        return new BotApiClient(httpFactory.CreateClient("bots"), cfg[$"{Section(m)}:ApiBase"] ?? DefaultBase(m), token.Trim(), chatId);
    }

    /// <summary>Webhook secret Telegram echoes in <c>X-Telegram-Bot-Api-Secret-Token</c>; null disables the webhook.</summary>
    public string? WebhookSecret(Messenger m) => cfg[$"{Section(m)}:WebhookSecret"] is { Length: >= 16 } s ? s : null;

    /// <summary>"Polling" pulls updates with getUpdates (local testing, no public address); default is webhook.</summary>
    public bool Polling(Messenger m) => string.Equals(cfg[$"{Section(m)}:Mode"], "Polling", StringComparison.OrdinalIgnoreCase);

    public string BaseUrl => (cfg["Site:BaseUrl"] ?? "http://localhost").TrimEnd('/');

    public static string WebhookPath(Messenger m) => $"/bot/{Section(m).ToLowerInvariant()}/webhook";
}
