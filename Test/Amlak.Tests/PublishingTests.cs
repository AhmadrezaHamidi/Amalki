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
        var json = method is "sendMessage" or "sendPhoto"
            ? "{\"ok\":true,\"result\":{\"message_id\":" + _nextId++ + "}}"
            : """{"ok":true,"result":true}""";
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
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
            new Env(_root), NullLogger<ChannelPublisher>.Instance);
    }

    private static Property Sample(ListingStatus status = ListingStatus.Published) => new()
    {
        Title = "آپارتمان تست", Slug = "آپارتمان-تست", Description = "d", Deal = DealType.Sale, Kind = PropertyKind.Apartment,
        City = "تهران", District = "ونک", AreaM2 = 90, Price = 5_000_000_000, Status = status, PublishedAtUtc = DateTime.UtcNow,
    };

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

        p.Status = ListingStatus.Draft; db.SaveChanges();
        await pub.SyncAsync(p.Id);

        Assert.Contains(_bot.Calls, c => c.Method == "deleteMessage");
        Assert.Empty(db.ChannelPosts);
    }

    [Fact]
    public async Task Photo_listing_uses_sendPhoto_and_edits_the_caption()
    {
        using var db = NewDb();
        File.WriteAllBytes(Path.Combine(_root, "uploads", "a.png"), [0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0, 0]);
        var p = Sample(); p.Photos.Add(new PropertyPhoto { Url = "/uploads/a.png", Alt = "x" });
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
}
