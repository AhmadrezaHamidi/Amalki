using Amlak.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Services.Publishing;

/// <summary>
/// Keeps Telegram and Bale channels in sync with a listing: publish when it goes live,
/// edit when it changes, delete the channel message when it is unpublished or removed.
/// Failures are recorded on <see cref="ChannelPost.Error"/> and never block the admin action.
/// </summary>
public class ChannelPublisher
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ChannelPublisher> _log;
    private readonly string _baseUrl;
    private readonly Dictionary<Channel, BotApiClient> _clients = [];
    private readonly HashSet<Channel> _buttons = [];

    public ChannelPublisher(AppDbContext db, IHttpClientFactory httpFactory, IConfiguration cfg,
        IWebHostEnvironment env, ILogger<ChannelPublisher> log)
    {
        _db = db; _env = env; _log = log;
        _baseUrl = cfg["Site:BaseUrl"] ?? "http://localhost";

        var http = httpFactory.CreateClient("bots");
        Register(Channel.Telegram, "Telegram", "https://api.telegram.org");
        Register(Channel.Bale, "Bale", "https://tapi.bale.ai");

        void Register(Channel channel, string section, string defaultBase)
        {
            var token = cfg[$"{section}:BotToken"];
            var chat = cfg[$"{section}:ChatId"];
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chat)) return;
            _clients[channel] = new BotApiClient(http, cfg[$"{section}:ApiBase"] ?? defaultBase, token, chat);
            // The "view listing" button is on unless explicitly turned off for a messenger that rejects it.
            if (!string.Equals(cfg[$"{section}:InlineButton"], "false", StringComparison.OrdinalIgnoreCase)) _buttons.Add(channel);
        }
    }

    public IReadOnlyCollection<Channel> ConfiguredChannels => _clients.Keys;

    public async Task SyncAsync(int propertyId, CancellationToken ct = default)
    {
        if (_clients.Count == 0) return;
        var p = await _db.Properties.Include(x => x.Photos).Include(x => x.ChannelPosts)
            .FirstOrDefaultAsync(x => x.Id == propertyId, ct);
        if (p is null) return;

        foreach (var (channel, client) in _clients)
        {
            var post = p.ChannelPosts.FirstOrDefault(c => c.Channel == channel);
            if (p.Status == ListingStatus.Published)
                await PublishOrEditAsync(p, post, channel, client, ct);
            else if (post is not null)
                await RemoveAsync(p, post, client, ct);
        }
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Deletes every channel message of a listing that is about to be removed.</summary>
    public async Task RemoveAllAsync(int propertyId, CancellationToken ct = default)
    {
        var posts = await _db.ChannelPosts.Where(c => c.PropertyId == propertyId).ToListAsync(ct);
        foreach (var post in posts.Where(x => x.ExternalMessageId is not null))
            if (_clients.TryGetValue(post.Channel, out var client))
                await client.DeleteAsync(post.ExternalMessageId!, ct);
    }

    private async Task PublishOrEditAsync(Property p, ChannelPost? post, Channel channel, BotApiClient client, CancellationToken ct)
    {
        var caption = CaptionBuilder.Build(p, _baseUrl);
        var markup = _buttons.Contains(channel) ? BotApiClient.LinkButton("مشاهده و درخواست تماس", CaptionBuilder.Url(p, _baseUrl)) : null;

        if (post?.ExternalMessageId is { } messageId)
        {
            var edit = await client.EditAsync(messageId, caption, post.HasPhoto, markup, ct);
            // Telegram answers "message is not modified" when nothing changed; that is success for us.
            var unchanged = edit.Error?.Contains("not modified", StringComparison.OrdinalIgnoreCase) == true;
            post.Error = edit.Ok || unchanged ? null : edit.Error;
            return;
        }

        var photoPath = p.Photos.OrderBy(x => x.SortOrder).Select(x => LocalPath(x.Url)).FirstOrDefault(File.Exists);
        var send = photoPath is not null
            ? await client.SendPhotoAsync(photoPath, caption, markup, ct)
            : await client.SendMessageAsync(caption, markup, ct);

        if (post is null)
        {
            post = new ChannelPost { PropertyId = p.Id, Channel = channel };
            _db.ChannelPosts.Add(post);
            p.ChannelPosts.Add(post);
        }
        post.ExternalMessageId = send.Ok ? send.MessageId : null;
        post.HasPhoto = send.Ok && photoPath is not null;
        post.Error = send.Ok ? null : send.Error;
        if (!send.Ok) _log.LogWarning("Publish to {Channel} failed for property {Id}: {Error}", channel, p.Id, send.Error);
    }

    private async Task RemoveAsync(Property p, ChannelPost post, BotApiClient client, CancellationToken ct)
    {
        if (post.ExternalMessageId is { } id)
        {
            var del = await client.DeleteAsync(id, ct);
            var gone = del.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true;
            if (!del.Ok && !gone)
            {
                post.Error = del.Error;
                return;
            }
        }
        _db.ChannelPosts.Remove(post);
        p.ChannelPosts.Remove(post);
    }

    private string LocalPath(string url) => Path.Combine(_env.WebRootPath, "uploads", Path.GetFileName(url));
}
