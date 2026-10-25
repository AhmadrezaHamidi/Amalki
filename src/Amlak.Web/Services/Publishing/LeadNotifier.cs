namespace Amlak.Web.Services.Publishing;

/// <summary>
/// Sends each new contact request to the admin's private chat on Telegram and/or Bale
/// (same bot token as the channel, <c>{Section}:AdminChatId</c> as the target).
/// A failure is logged and never fails the visitor's request: the lead is already saved.
/// </summary>
public class LeadNotifier
{
    private readonly List<(Channel Channel, BotApiClient Client)> _clients = [];
    private readonly ILogger<LeadNotifier> _log;
    private readonly string _baseUrl;

    public LeadNotifier(IHttpClientFactory httpFactory, IConfiguration cfg, ILogger<LeadNotifier> log)
    {
        _log = log;
        _baseUrl = cfg["Site:BaseUrl"] ?? "http://localhost";
        var http = httpFactory.CreateClient("bots");
        Register(Channel.Telegram, "Telegram", "https://api.telegram.org");
        Register(Channel.Bale, "Bale", "https://tapi.bale.ai");

        void Register(Channel channel, string section, string defaultBase)
        {
            var token = cfg[$"{section}:BotToken"];
            var chat = cfg[$"{section}:AdminChatId"];
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chat)) return;
            _clients.Add((channel, new BotApiClient(http, cfg[$"{section}:ApiBase"] ?? defaultBase, token, chat)));
        }
    }

    public bool Configured => _clients.Count > 0;

    public async Task NotifyAsync(Lead lead, Property? property, CancellationToken ct = default)
    {
        var text = CaptionBuilder.Lead(lead, property, _baseUrl);
        foreach (var (channel, client) in _clients)
        {
            var r = await client.SendMessageAsync(text, ct: ct);
            if (!r.Ok) _log.LogWarning("Lead notification to {Channel} failed: {Error}", channel, r.Error);
        }
    }
}
