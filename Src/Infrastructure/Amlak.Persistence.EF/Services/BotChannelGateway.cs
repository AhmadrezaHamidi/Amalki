using Amlak.Application.Contract.Services;
using Amlak.Domain.Publishing.Enums;
using Microsoft.Extensions.Configuration;

namespace Amlak.Persistence.EF.Services;

/// <summary>
/// درگاه کانال‌ها از روی تنظیمات <c>Telegram:*</c> و <c>Bale:*</c>.
/// <c>ApiBase</c> قابل تغییر است چون تلگرام در ایران فیلتر است و ممکن است از واسط استفاده شود.
/// </summary>
public sealed class BotChannelGateway : IChannelGateway
{
    private readonly Dictionary<Channel, BotApiClient> _clients = [];

    public BotChannelGateway(IConfiguration configuration, HttpClient http)
    {
        Register(Channel.Telegram, "Telegram", "https://api.telegram.org");
        Register(Channel.Bale,     "Bale",     "https://tapi.bale.ai");

        void Register(Channel channel, string section, string defaultBase)
        {
            var token = configuration[$"{section}:BotToken"];
            var chat  = configuration[$"{section}:ChatId"];
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chat)) return;

            var apiBase = configuration[$"{section}:ApiBase"];
            _clients[channel] = new BotApiClient(http, string.IsNullOrWhiteSpace(apiBase) ? defaultBase : apiBase, token, chat);
        }
    }

    public IReadOnlyCollection<Channel> EnabledChannels => _clients.Keys;

    public Task<ChannelResult> SendAsync(Channel channel, string caption, string? photoPath, CancellationToken ct = default)
        => photoPath is null
            ? Client(channel).SendMessageAsync(caption, ct)
            : Client(channel).SendPhotoAsync(photoPath, caption, ct);

    public Task<ChannelResult> EditAsync(Channel channel, string messageId, string caption, bool isPhoto, CancellationToken ct = default)
        => Client(channel).EditAsync(messageId, caption, isPhoto, ct);

    public Task<ChannelResult> DeleteAsync(Channel channel, string messageId, CancellationToken ct = default)
        => Client(channel).DeleteAsync(messageId, ct);

    private BotApiClient Client(Channel channel)
        => _clients.TryGetValue(channel, out var c) ? c : throw new InvalidOperationException($"کانال {channel} پیکربندی نشده است");
}
