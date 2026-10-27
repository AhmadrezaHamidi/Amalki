using System.Net;
using Amlak.Web.Data;
using Amlak.Web.Domain;
using Amlak.Web.Services.Publishing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace Amlak.Tests;

/// <summary>Records every Bot API call and answers like Telegram would.</summary>
internal sealed class FakeBotHandler : HttpMessageHandler
{
    public List<(string Method, string Url, string Body)> Calls { get; } = [];
    public Func<string, HttpResponseMessage>? Override { get; set; }
    private int _nextId = 100;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        var url = req.RequestUri!.ToString();
        var body = req.Content is null ? "" : await req.Content.ReadAsStringAsync(ct);
        Calls.Add((url.Split('/').Last(), url, body));
        var method = url.Split('/').Last();
        if (Override?.Invoke(method) is { } custom) return custom;
        var json = method switch
        {
            "sendMessage" or "sendPhoto" => "{\"ok\":true,\"result\":{\"message_id\":" + _nextId++ + "}}",
            // One message per attached photo, like Telegram.
            "sendMediaGroup" => "{\"ok\":true,\"result\":[" + string.Join(",", Enumerable.Range(0, System.Text.RegularExpressions.Regex.Matches(body, "attach://").Count)
                .Select(_ => "{\"message_id\":" + _nextId++ + "}")) + "]}",
            _ => """{"ok":true,"result":true}""",
        };
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
    }
}

internal static class TestStores
{
    public static Amlak.Web.Services.PhotoStorage Photos(string root)
    {
        var local = new Amlak.Web.Services.Storage.LocalFileStore(new Env(root));
        return new Amlak.Web.Services.PhotoStorage(local, local);
    }
}

internal sealed class Factory(HttpMessageHandler h) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(h, disposeHandler: false);
}

internal sealed class Env(string root) : IWebHostEnvironment
{
    public string WebRootPath { get; set; } = root;
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string ApplicationName { get; set; } = "t";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = root;
    public string EnvironmentName { get; set; } = "Test";
}

public sealed class PublishingTests : IDisposable
{
    private const string Token = "123:SECRET-TOKEN";
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly FakeBotHandler _bot = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), "amlak-" + Guid.NewGuid().ToString("N"));

    public PublishingTests()
    {
        _conn.Open();
        Directory.CreateDirectory(Path.Combine(_root, "uploads"));
    }

    public void Dispose()
    {
        _conn.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private AppDbContext NewDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options);
        db.Database.EnsureCreated();
        return db;
    }

    private ChannelPublisher NewPublisher(AppDbContext db, bool telegram = true, bool bale = false)
    {
        var cfg = new Dictionary<string, string?> { ["Site:BaseUrl"] = "https://example.ir" };
        if (telegram) { cfg["Telegram:BotToken"] = Token; cfg["Telegram:ChatId"] = "@chan"; }
        if (bale) { cfg["Bale:BotToken"] = "999:BALE"; cfg["Bale:ChatId"] = "42"; }
        return new ChannelPublisher(db, new Factory(_bot), new ConfigurationBuilder().AddInMemoryCollection(cfg).Build(),
            TestStores.Photos(_root), NullLogger<ChannelPublisher>.Instance);
    }

    private static Property Sample(ListingStatus status = ListingStatus.Published) =>
        TestData.Listing(published: status == ListingStatus.Published);

    [Fact]
    public void Caption_has_price_link_and_hashtags()
    {
        var text = CaptionBuilder.Build(Sample(), "https://example.ir/");
        Assert.Contains("۵ میلیارد", text);
        Assert.Contains("https://example.ir/ملک/" + Uri.EscapeDataString("آپارتمان-تست"), text);
        Assert.Contains("#فروش", text);
        Assert.Contains("#تهران", text);
    }

    [Fact]
    public async Task Publishing_sends_once_then_edits_instead_of_reposting()
    {
        using var db = NewDb();
        var p = Sample(); db.Properties.Add(p); db.SaveChanges();
        var pub = NewPublisher(db);

        await pub.SyncAsync(p.Id);
        await pub.SyncAsync(p.Id);

        Assert.Equal(1, _bot.Calls.Count(c => c.Method == "sendMessage"));
        Assert.Equal(1, _bot.Calls.Count(c => c.Method == "editMessageText"));
        var post = db.ChannelPosts.Single();
        Assert.Equal("100", post.ExternalMessageId);
        Assert.Null(post.Error);
    }

    [Fact]
    public async Task Draft_listing_is_not_published()
    {
        using var db = NewDb();
        var p = Sample(ListingStatus.Draft); db.Properties.Add(p); db.SaveChanges();
        await NewPublisher(db).SyncAsync(p.Id);
        Assert.Empty(_bot.Calls);
        Assert.Empty(db.ChannelPosts);
    }

    [Fact]
    public async Task Unpublishing_deletes_the_channel_message_and_the_record()
    {
        using var db = NewDb();
        var p = Sample(); db.Properties.Add(p); db.SaveChanges();
        var pub = NewPublisher(db);
        await pub.SyncAsync(p.Id);

        p.Unpublish(TestData.Now); db.SaveChanges();
        await pub.SyncAsync(p.Id);

        Assert.Contains(_bot.Calls, c => c.Method == "deleteMessage");
        Assert.Empty(db.ChannelPosts);
    }

    [Fact]
    public async Task Photo_listing_uses_sendPhoto_and_edits_the_caption()
    {
        using var db = NewDb();
        File.WriteAllBytes(Path.Combine(_root, "uploads", "a.png"), [0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0, 0]);
        var p = Sample(); p.AddPhoto("/uploads/a.png", TestData.Now);
        db.Properties.Add(p); db.SaveChanges();
        var pub = NewPublisher(db);

        await pub.SyncAsync(p.Id);
        await pub.SyncAsync(p.Id);

        Assert.Contains(_bot.Calls, c => c.Method == "sendPhoto");
        Assert.Contains(_bot.Calls, c => c.Method == "editMessageCaption");
        Assert.True(db.ChannelPosts.Single().HasPhoto);
    }

    [Fact]
    public async Task Failure_is_recorded_without_leaking_the_token_and_retried_later()
    {
        using var db = NewDb();
        var p = Sample(); db.Properties.Add(p); db.SaveChanges();
        var pub = NewPublisher(db);

        _bot.Override = _ => new HttpResponseMessage(HttpStatusCode.Forbidden)
        { Content = new StringContent("{\"ok\":false,\"description\":\"Forbidden: bot " + Token + " is not a member\"}") };
        await pub.SyncAsync(p.Id);

        var post = db.ChannelPosts.Single();
        Assert.Null(post.ExternalMessageId);
        Assert.NotNull(post.Error);
        Assert.DoesNotContain("SECRET-TOKEN", post.Error);

        _bot.Override = null;
        await pub.SyncAsync(p.Id);
        Assert.NotNull(db.ChannelPosts.Single().ExternalMessageId);
        Assert.Null(db.ChannelPosts.Single().Error);
    }

    [Fact]
    public async Task Both_channels_get_their_own_message_and_use_their_own_api_host()
    {
        using var db = NewDb();
        var p = Sample(); db.Properties.Add(p); db.SaveChanges();
        await NewPublisher(db, telegram: true, bale: true).SyncAsync(p.Id);

        Assert.Equal(2, db.ChannelPosts.Count());
        Assert.Contains(_bot.Calls, c => c.Url.StartsWith("https://api.telegram.org/bot"));
        Assert.Contains(_bot.Calls, c => c.Url.StartsWith("https://tapi.bale.ai/bot"));
    }

    [Fact]
    public async Task No_configured_channel_is_a_noop()
    {
        using var db = NewDb();
        var p = Sample(); db.Properties.Add(p); db.SaveChanges();
        await NewPublisher(db, telegram: false).SyncAsync(p.Id);
        Assert.Empty(_bot.Calls);
    }

    private Property WithPhotos(int n)
    {
        var p = Sample();
        for (var i = 0; i < n; i++)
        {
            File.WriteAllBytes(Path.Combine(_root, "uploads", $"p{i}.png"), [0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0, 0]);
            p.AddPhoto($"/uploads/p{i}.png", TestData.Now);
        }
        return p;
    }

    [Fact]
    public async Task Several_photos_go_out_as_one_album_and_all_its_messages_are_tracked()
    {
        using var db = NewDb();
        var p = WithPhotos(3); db.Properties.Add(p); db.SaveChanges();
        await NewPublisher(db).SyncAsync(p.Id);

        var call = Assert.Single(_bot.Calls);
        Assert.Equal("sendMediaGroup", call.Method);
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(call.Body, "attach://p").Count);
        var post = db.ChannelPosts.Single();
        Assert.Equal("100", post.ExternalMessageId);
        Assert.Equal("101,102", post.ExtraMessageIds);
        Assert.True(post.HasPhoto);
    }

    [Fact]
    public async Task Adding_a_photo_replaces_the_album_and_unpublishing_deletes_every_message()
    {
        using var db = NewDb();
        var p = WithPhotos(2); db.Properties.Add(p); db.SaveChanges();
        var pub = NewPublisher(db);
        await pub.SyncAsync(p.Id);

        File.WriteAllBytes(Path.Combine(_root, "uploads", "extra.png"), [0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0, 0]);
        p.AddPhoto("/uploads/extra.png", TestData.Now); db.SaveChanges();
        await pub.SyncAsync(p.Id);

        // Old album (2 messages) deleted, new album of 3 sent.
        Assert.Equal(2, _bot.Calls.Count(c => c.Method == "deleteMessage"));
        Assert.Equal(2, _bot.Calls.Count(c => c.Method == "sendMediaGroup"));
        Assert.Equal("103,104", db.ChannelPosts.Single().ExtraMessageIds);

        _bot.Calls.Clear();
        p.Unpublish(TestData.Now); db.SaveChanges();
        await pub.SyncAsync(p.Id);
        Assert.Equal(3, _bot.Calls.Count(c => c.Method == "deleteMessage"));
        Assert.Empty(db.ChannelPosts);
    }

    [Fact]
    public async Task Text_change_on_an_album_edits_its_caption_without_a_keyboard()
    {
        using var db = NewDb();
        var p = WithPhotos(2); db.Properties.Add(p); db.SaveChanges();
        var pub = NewPublisher(db);
        await pub.SyncAsync(p.Id);
        p.Update(ListingDetails.Of("عنوان تازه", "d", p.Kind, p.AreaM2, p.City, p.District), p.Pricing, TestData.Now); db.SaveChanges();
        await pub.SyncAsync(p.Id);

        var edit = Assert.Single(_bot.Calls, c => c.Method == "editMessageCaption");
        Assert.DoesNotContain("reply_markup", edit.Body);
        Assert.Single(_bot.Calls, c => c.Method == "sendMediaGroup");
    }
}
