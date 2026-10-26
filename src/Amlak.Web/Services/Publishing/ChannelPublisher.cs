using System.Security.Cryptography;
using System.Text;
using Amlak.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Services.Publishing;

/// <summary>
/// Keeps Telegram and Bale channels in sync with a listing: publish when it goes live (one photo with
/// caption and button, an album when it has several photos, or plain text), edit the caption when the
/// text changes, replace the post when the photos change, delete it when unpublished or removed.
/// Failures are recorded on <see cref="ChannelPost.Error"/> and never block the admin action.
/// </summary>
public class ChannelPublisher
{
    private readonly AppDbContext _db;
    private readonly PhotoStorage _photos;
    private readonly ILogger<ChannelPublisher> _log;
    private readonly string _baseUrl;
    private readonly Dictionary<Channel, BotApiClient> _clients = [];
    private readonly HashSet<Channel> _buttons = [];
    private readonly HashSet<Channel> _albums = [];

    public ChannelPublisher(AppDbContext db, IHttpClientFactory httpFactory, IConfiguration cfg,
        PhotoStorage photos, ILogger<ChannelPublisher> log)
    {
        _db = db; _photos = photos; _log = log;
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
            // Both are on unless explicitly turned off for a messenger that rejects them.
            if (!IsOff(cfg[$"{section}:InlineButton"])) _buttons.Add(channel);
            if (!IsOff(cfg[$"{section}:Albums"])) _albums.Add(channel);
        }

        static bool IsOff(string? v) => string.Equals(v, "false", StringComparison.OrdinalIgnoreCase);
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
        foreach (var post in posts) await DeleteMessagesAsync(post.Channel, MessageIds(post), ct);
    }

    /// <summary>Deletes messages of a listing that no longer exists; a message already gone counts as deleted.</summary>
    public async Task DeleteMessagesAsync(Channel channel, IEnumerable<string> messageIds, CancellationToken ct = default)
    {
        if (!_clients.TryGetValue(channel, out var client)) return;
        foreach (var id in messageIds) await client.DeleteAsync(id, ct);
    }

    /// <summary>Every message id of a post: the first message, then the rest of its album.</summary>
    public static IEnumerable<string> MessageIdsOf(ChannelPost post) => MessageIds(post);

    private async Task PublishOrEditAsync(Property p, ChannelPost? post, Channel channel, BotApiClient client, CancellationToken ct)
    {
        var caption = CaptionBuilder.Build(p, _baseUrl);
        var markup = _buttons.Contains(channel) ? BotApiClient.LinkButton("مشاهده و درخواست تماس", CaptionBuilder.Url(p, _baseUrl)) : null;
        var photos = p.Photos.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).Take(BotApiClient.MaxAlbum).ToList();
        var key = PhotoKey(photos);

        if (post?.ExternalMessageId is { } messageId)
        {
            // Posts made before photo tracking have no key: adopt the current photos as posted rather than
            // reposting every listing on upgrade; later photo changes are then detected as usual.
            if (post.PhotoKey is null || post.PhotoKey == key)
            {
                post.PhotoKey ??= key;
                // An album cannot carry a keyboard, so its caption is edited without one.
                var isAlbum = post.ExtraMessageIds is not null;
                var edit = await client.EditAsync(messageId, caption, post.HasPhoto, isAlbum ? null : markup, ct);
                // Telegram answers "message is not modified" when nothing changed; that is success for us.
                var unchanged = edit.Error?.Contains("not modified", StringComparison.OrdinalIgnoreCase) == true;
                post.Error = edit.Ok || unchanged ? null : edit.Error;
                return;
            }

            // Photos were added or removed: messengers cannot change an album in place, so replace the post.
            foreach (var id in MessageIds(post)) await client.DeleteAsync(id, ct);
            post.ExternalMessageId = null;
            post.ExtraMessageIds = null;
        }

        var files = new List<BotPhoto>();
        try
        {
            foreach (var photo in photos)
                if (await _photos.OpenAsync(photo.Url, ct) is { } stream)
                    files.Add(new BotPhoto(stream, Path.GetFileName(photo.Url.Split('?')[0])));

            var send = files.Count switch
            {
                0 => await client.SendMessageAsync(caption, markup, ct),
                1 => await client.SendPhotoAsync(files[0], caption, markup, ct),
                _ when _albums.Contains(channel) => await client.SendMediaGroupAsync(files, caption, ct),
                _ => await client.SendPhotoAsync(files[0], caption, markup, ct),
            };

            if (post is null)
            {
                post = new ChannelPost { PropertyId = p.Id, Channel = channel };
                _db.ChannelPosts.Add(post);
                // EF already links a tracked post to its listing; add it only when it did not.
                if (!p.ChannelPosts.Contains(post)) p.ChannelPosts.Add(post);
            }
            var ids = send.Ok ? send.MessageIds : [];
            post.ExternalMessageId = ids.FirstOrDefault();
            post.ExtraMessageIds = ids.Count > 1 ? string.Join(",", ids.Skip(1)) : null;
            post.HasPhoto = send.Ok && files.Count > 0;
            post.PhotoKey = send.Ok ? key : null;
            post.Error = send.Ok ? null : send.Error;
            if (!send.Ok) _log.LogWarning("Publish to {Channel} failed for property {Id}: {Error}", channel, p.Id, send.Error);
        }
        finally
        {
            foreach (var f in files) await f.Content.DisposeAsync();
        }
    }

    private async Task RemoveAsync(Property p, ChannelPost post, BotApiClient client, CancellationToken ct)
    {
        foreach (var id in MessageIds(post))
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

    private static IEnumerable<string> MessageIds(ChannelPost post)
    {
        if (post.ExternalMessageId is { } first) yield return first;
        foreach (var id in (post.ExtraMessageIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)) yield return id;
    }

    private static string PhotoKey(IEnumerable<PropertyPhoto> photos) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", photos.Select(x => x.Url)))))[..32];
}
