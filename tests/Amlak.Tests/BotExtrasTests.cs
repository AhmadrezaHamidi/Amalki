using Amlak.Web.Data;
using Amlak.Web.Domain;
using Amlak.Web.Services;
using Amlak.Web.Services.Publishing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Amlak.Tests;

/// <summary>Inline "view listing" button on channel posts, admin lead alerts, and phone normalization.</summary>
public sealed class BotExtrasTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly FakeBotHandler _bot = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), "amlak-" + Guid.NewGuid().ToString("N"));

    public BotExtrasTests()
    {
        _conn.Open();
        Directory.CreateDirectory(Path.Combine(_root, "uploads"));
    }

    public void Dispose()
    {
        _conn.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private static IConfiguration Cfg(params (string, string)[] extra)
    {
        var cfg = new Dictionary<string, string?>
        {
            ["Site:BaseUrl"] = "https://example.ir", ["Telegram:BotToken"] = "123:SECRET", ["Telegram:ChatId"] = "@chan",
        };
        foreach (var (k, v) in extra) cfg[k] = v;
        return new ConfigurationBuilder().AddInMemoryCollection(cfg).Build();
    }

    private (AppDbContext Db, Property P) Seed()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options);
        db.Database.EnsureCreated();
        var p = new Property
        {
            Title = "ویلا", Slug = "ویلا", Description = "d", Deal = DealType.Sale, Kind = PropertyKind.Villa,
            City = "کرج", District = "مهرشهر", AreaM2 = 300, Price = 9_000_000_000, Status = ListingStatus.Published,
        };
        db.Properties.Add(p); db.SaveChanges();
        return (db, p);
    }

    [Fact]
    public async Task Channel_post_and_its_edit_carry_the_listing_button()
    {
        var (db, p) = Seed();
        using var _ = db;
        var pub = new ChannelPublisher(db, new Factory(_bot), Cfg(), new Env(_root), NullLogger<ChannelPublisher>.Instance);

        await pub.SyncAsync(p.Id);
        await pub.SyncAsync(p.Id);

        Assert.All(_bot.Calls, c => Assert.Contains("inline_keyboard", c.Body));
        Assert.Contains(_bot.Calls, c => c.Body.Contains(CaptionBuilder.Url(p, "https://example.ir")));
    }

    [Fact]
    public async Task Button_can_be_turned_off_per_messenger()
    {
        var (db, p) = Seed();
        using var _ = db;
        var pub = new ChannelPublisher(db, new Factory(_bot), Cfg(("Telegram:InlineButton", "false")), new Env(_root), NullLogger<ChannelPublisher>.Instance);
        await pub.SyncAsync(p.Id);
        Assert.DoesNotContain("reply_markup", _bot.Calls.Single().Body);
    }

    [Fact]
    public async Task Lead_goes_to_the_admin_chat_not_the_channel()
    {
        var (db, p) = Seed();
        using var _ = db;
        var notifier = new LeadNotifier(new Factory(_bot), Cfg(("Telegram:AdminChatId", "777")), NullLogger<LeadNotifier>.Instance);

        await notifier.NotifyAsync(new Lead { Name = "سارا", Phone = "09121112233", Message = "عصر" }, p);

        var call = Assert.Single(_bot.Calls);
        Assert.Equal("sendMessage", call.Method);
        Assert.Contains("777", call.Body);
        Assert.DoesNotContain("@chan", call.Body);
        Assert.Contains("09121112233", call.Body);
        Assert.Contains("ویلا", call.Body);
    }

    [Fact]
    public async Task Without_admin_chat_no_lead_message_is_sent()
    {
        var notifier = new LeadNotifier(new Factory(_bot), Cfg(), NullLogger<LeadNotifier>.Instance);
        Assert.False(notifier.Configured);
        await notifier.NotifyAsync(new Lead { Name = "x", Phone = "09120000000" }, null);
        Assert.Empty(_bot.Calls);
    }

    [Theory]
    [InlineData("۰۹۱۲ ۱۱۱ ۲۲۳۳", "09121112233")]
    [InlineData("+98 912 111 2233", "09121112233")]
    [InlineData("9121112233", "09121112233")]
    [InlineData("٠٩١٢١١١٢٢٣٣", "09121112233")]
    [InlineData("021-12345678", null)]
    [InlineData("123", null)]
    public void Mobile_numbers_are_normalized(string input, string? expected) => Assert.Equal(expected, Fa.Mobile(input));
}
