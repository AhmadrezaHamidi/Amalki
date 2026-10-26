namespace Amlak.Web.Services.Publishing;

/// <summary>
/// Sends new contact requests and support-ticket activity to the admin's private chat on Telegram
/// and/or Bale (same bot token as the channel, <c>{Section}:AdminChatId</c> as the target).
/// A failure is logged and never fails the visitor's request: the data is already saved.
/// </summary>
public class AdminNotifier
{
    private readonly List<(Channel Channel, BotApiClient Client)> _clients = [];
    private readonly ILogger<AdminNotifier> _log;
    private readonly string _baseUrl;

    public AdminNotifier(IHttpClientFactory httpFactory, IConfiguration cfg, ILogger<AdminNotifier> log)
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

    public Task NotifyAsync(Lead lead, Property? property, CancellationToken ct = default) =>
        SendAsync(CaptionBuilder.Lead(lead, property, _baseUrl), ct);

    /// <param name="isNew">True for a new ticket, false for a visitor's follow-up message.</param>
    public Task NotifyTicketAsync(Ticket ticket, string message, bool isNew, CancellationToken ct = default) =>
        SendAsync(CaptionBuilder.Ticket(ticket, message, isNew, _baseUrl), ct);

    private async Task SendAsync(string text, CancellationToken ct)
    {
        foreach (var (channel, client) in _clients)
        {
            var r = await client.SendMessageAsync(text, ct: ct);
            if (!r.Ok) _log.LogWarning("Admin notification to {Channel} failed: {Error}", channel, r.Error);
        }
    }
}
