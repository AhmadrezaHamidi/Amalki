using System.Net;
using Amlak.Web.Data;
using Amlak.Web.Domain;
using Amlak.Web.Services.Events;
using Amlak.Web.Services.Publishing;
using Amlak.Web.Services.Sms;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Amlak.Tests;

internal sealed class ManualClock(DateTime start) : TimeProvider
{
    public DateTime UtcNow { get; set; } = start;
    public override DateTimeOffset GetUtcNow() => new(UtcNow, TimeSpan.Zero);
}

/// <summary>Domain events are saved with the change and handled later by the dispatcher.</summary>
public sealed class OutboxTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly FakeBotHandler _bot = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), "amlak-" + Guid.NewGuid().ToString("N"));
    private readonly ManualClock _clock = new(TestData.Now);
    private readonly RecordingSms _sms = new();

    public OutboxTests()
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

    private OutboxDispatcher Dispatcher(AppDbContext db)
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Site:BaseUrl"] = "https://example.ir", ["Telegram:BotToken"] = "123:SECRET", ["Telegram:ChatId"] = "@chan",
            ["Telegram:AdminChatId"] = "777",
        }).Build();
        var http = new Factory(_bot);
        var photos = TestStores.Photos(_root);
        return new OutboxDispatcher(db,
            new ChannelPublisher(db, http, cfg, photos, NullLogger<ChannelPublisher>.Instance),
            new AdminNotifier(http, cfg, NullLogger<AdminNotifier>.Instance),
            photos, new AlertService(db, new SmsGateway(db, _sms, _clock), cfg, _clock),
            _clock, NullLogger<OutboxDispatcher>.Instance);
    }

    [Fact]
    public async Task Publishing_texts_matching_sms_subscribers_once()
    {
        using var db = NewDb();
        db.SavedSearches.Add(new SavedSearch { Phone = "09121234567", Deal = DealType.Sale, City = "تهران", CreatedAtUtc = TestData.Now.AddDays(-1) });
        db.SaveChanges();
        var p = TestData.Listing(published: false);
        db.Properties.Add(p);
        p.Publish(TestData.Now);
        db.SaveChanges();

        await Dispatcher(db).ProcessBatchAsync();
        p.ShowPriceAs(PriceDisplay.TotalOnly, TestData.Now); // a change at the same price sends nothing more
        db.SaveChanges();
        await Dispatcher(db).ProcessBatchAsync();

        var text = Assert.Single(_sms.Texts);
        Assert.Equal("09121234567", text.Mobile);
        Assert.Contains($"https://example.ir/p/{p.Id}", text.Text);
    }

    [Fact]
    public void Saving_writes_one_row_per_event_with_the_new_id_and_clears_the_events()
    {
        using var db = NewDb();
        var p = Property.Create("a", ListingDetails.Of("آپارتمان", "d", PropertyKind.Apartment, 90, "تهران", "ونک"), Pricing.ForSale(5), TestData.Now);
        p.Publish(TestData.Now);
        db.Properties.Add(p);
        db.SaveChanges();

        var rows = db.OutboxMessages.OrderBy(m => m.Id).ToList();
        Assert.Equal([nameof(ListingCreated), nameof(ListingPublished)], rows.Select(r => r.Type));
        Assert.All(rows, r => Assert.Equal(p.Id, r.AggregateId));
        Assert.All(rows, r => Assert.Null(r.ProcessedAtUtc));
        Assert.Empty(p.DomainEvents);
    }

    [Fact]
    public async Task Publishing_reaches_the_channel_only_through_the_dispatcher_and_once_per_batch()
    {
        using var db = NewDb();
        var p = Property.Create("a", ListingDetails.Of("آپارتمان", "d", PropertyKind.Apartment, 90, "تهران", "ونک"), Pricing.ForSale(5), TestData.Now);
        p.Publish(TestData.Now);
        db.Properties.Add(p);
        await db.SaveChangesAsync();
        Assert.Empty(_bot.Calls); // the save itself calls nothing

        Assert.Equal(2, await Dispatcher(db).ProcessBatchAsync());

        Assert.Equal("sendMessage", Assert.Single(_bot.Calls).Method);
        Assert.All(db.OutboxMessages, m => Assert.NotNull(m.ProcessedAtUtc));
        Assert.Equal(0, await Dispatcher(db).ProcessBatchAsync());
    }

    [Fact]
    public async Task A_failed_channel_call_is_retried_after_a_backoff()
    {
        using var db = NewDb();
        var p = TestData.Listing(published: false);
        db.Properties.Add(p); db.SaveChanges();
        p.Publish(TestData.Now); db.SaveChanges();

        _bot.Override = _ => new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("""{"ok":false,"description":"chat not found"}""") };
        await Dispatcher(db).ProcessBatchAsync();
        var row = db.OutboxMessages.Single();
        Assert.Null(row.ProcessedAtUtc);
        Assert.Equal(1, row.Attempts);
        Assert.Contains("chat not found", row.Error);
        Assert.Equal(TestData.Now + OutboxDispatcher.Backoff(1), row.NextAttemptAtUtc);

        // Not due yet: nothing is taken.
        Assert.Equal(0, await Dispatcher(db).ProcessBatchAsync());

        _bot.Override = null;
        _clock.UtcNow += OutboxDispatcher.Backoff(1);
        await Dispatcher(db).ProcessBatchAsync();
        db.ChangeTracker.Clear();
        Assert.NotNull(db.OutboxMessages.Single().ProcessedAtUtc);
        Assert.Null(db.ChannelPosts.Single().Error);
    }

    [Fact]
    public async Task Deleting_a_listing_removes_its_channel_messages_and_photo_files_after_the_save()
    {
        using var db = NewDb();
        var p = TestData.Listing();
        File.WriteAllBytes(Path.Combine(_root, "uploads", "a.png"), [0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0, 0]);
        p.AddPhoto("/uploads/a.png", TestData.Now);
        db.Properties.Add(p); db.SaveChanges();
        await Dispatcher(db).ProcessBatchAsync(); // posts the photo
        _bot.Calls.Clear();

        var loaded = db.Properties.Include(x => x.Photos).Include(x => x.ChannelPosts).Single();
        loaded.MarkDeleted(TestData.Now);
        db.Properties.Remove(loaded);
        db.SaveChanges();
        Assert.True(File.Exists(Path.Combine(_root, "uploads", "a.png"))); // nothing happens inside the save

        await Dispatcher(db).ProcessBatchAsync();
        Assert.Equal("deleteMessage", Assert.Single(_bot.Calls).Method);
        Assert.False(File.Exists(Path.Combine(_root, "uploads", "a.png")));
    }

    [Fact]
    public async Task A_lead_and_a_ticket_alert_the_admin_chat()
    {
        using var db = NewDb();
        db.Leads.Add(TestData.Lead(message: "عصر"));
        db.Tickets.Add(Ticket.Open("K7Q2M9XA", "سارا", PhoneNumber.Parse("09121112233"), "سؤال", TicketCategory.General, "سلام", TestData.Now));
        await db.SaveChangesAsync();

        await Dispatcher(db).ProcessBatchAsync();

        Assert.Equal(2, _bot.Calls.Count(c => c.Method == "sendMessage" && c.Body.Contains("777")));
        Assert.Contains(_bot.Calls, c => c.Body.Contains("عصر"));
        Assert.Contains(_bot.Calls, c => c.Body.Contains("سلام"));
    }

    [Fact]
    public void Backoff_grows_and_is_capped_at_an_hour()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), OutboxDispatcher.Backoff(1));
        Assert.Equal(TimeSpan.FromMinutes(2), OutboxDispatcher.Backoff(3));
        Assert.Equal(TimeSpan.FromHours(1), OutboxDispatcher.Backoff(20));
    }
}
