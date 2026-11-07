using System.Text.Json;
using Amlak.Application.Support;
using Amlak.Domain;
using Amlak.Web.Data;
using Amlak.Web.Services.Bot;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Amlak.Tests;

/// <summary>The two-way bot works on the site's own tables: linking by phone, listing and answering tickets.</summary>
public sealed class BotTests : IDisposable
{
    private const string Me = "555", Phone = "09121234567";
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly FakeBotHandler _bot = new();
    private readonly ManualClock _clock = new(TestData.Now);
    private readonly IConfiguration _cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Site:BaseUrl"] = "https://example.ir", ["Telegram:BotToken"] = "123:SECRET",
    }).Build();
    private long _update = 1;

    public BotTests() => _conn.Open();
    public void Dispose() => _conn.Dispose();

    private AppDbContext NewDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options);
        db.Database.EnsureCreated();
        return db;
    }

    private BotUpdateHandler Handler(AppDbContext db) =>
        new(db, new TicketCommands(new EfTicketRepository(db), db, _clock), new BotClients(new Factory(_bot), _cfg), _clock,
            NullLogger<BotUpdateHandler>.Instance);

    private async Task<bool> Send(object body, long? id = null)
    {
        using var db = NewDb();
        var json = JsonSerializer.SerializeToElement(new Dictionary<string, object> { ["update_id"] = id ?? _update++, [body is Callback ? "callback_query" : "message"] = body is Callback c ? c.Value : body });
        return await Handler(db).HandleAsync(Messenger.Telegram, json);
    }

    private sealed record Callback(object Value);

    private static object Chat => new { id = 555, type = "private", first_name = "Mahsa" };
    private static object From => new { id = 555, first_name = "Mahsa" };
    private static object Text(string t) => new { message_id = 1, chat = Chat, from = From, text = t };
    private static object Contact(long userId, string phone = "+989121234567") =>
        new { message_id = 1, chat = Chat, from = From, contact = new { phone_number = phone, user_id = userId, first_name = "Mahsa" } };
    private static Callback Press(string data) =>
        new(new { id = "cb1", from = From, data, message = new { message_id = 9, chat = Chat } });

    private string OpenTicket(string phone = Phone)
    {
        using var db = NewDb();
        var t = Ticket.Open(TicketCodes.New(), "مهسا", PhoneNumber.Parse(phone), "سؤال قیمت", TicketCategory.General, "سلام", TestData.Now);
        db.Tickets.Add(t);
        db.SaveChanges();
        return t.Code;
    }

    private string LastText() => _bot.Calls.Last(c => c.Method == "sendMessage").Body;

    [Fact]
    public async Task Start_asks_for_the_phone_and_creates_an_account()
    {
        await Send(Text("/start"));
        Assert.Contains("request_contact", LastText());
        using var db = NewDb();
        var a = Assert.Single(db.MessengerAccounts);
        Assert.Equal(Me, a.ChatId);
        Assert.False(a.Linked);
    }

    [Fact]
    public async Task Own_contact_links_the_chat_to_the_site_phone()
    {
        OpenTicket();
        await Send(Contact(555));
        using var db = NewDb();
        Assert.Equal(Phone, db.MessengerAccounts.Single().Phone);
        Assert.Contains("۱ تیکت", LastText());
    }

    [Fact]
    public async Task Someone_elses_contact_is_refused()
    {
        await Send(Contact(999));
        using var db = NewDb();
        Assert.Null(db.MessengerAccounts.Single().Phone);
    }

    [Fact]
    public async Task Replying_from_the_bot_writes_to_the_same_ticket_the_site_shows()
    {
        var code = OpenTicket();
        await Send(Contact(555));
        await Send(Text(BotUpdateHandler.MyTickets));
        Assert.Contains("r:" + code, LastText());

        await Send(Press("r:" + code));
        await Send(Text("هنوز موجود است؟"));

        using var db = NewDb();
        var t = db.Tickets.Include(x => x.Messages).Single();
        Assert.Equal("هنوز موجود است؟", t.Messages.Last().Body);
        Assert.False(t.Messages.Last().FromStaff);
        Assert.Contains(db.OutboxMessages, m => m.Type == nameof(TicketMessageReceived)); // admin is alerted as for the site
        Assert.Null(db.MessengerAccounts.Single().ReplyingToTicket);
    }

    [Fact]
    public async Task Another_phones_ticket_cannot_be_answered()
    {
        var code = OpenTicket("09350000000");
        await Send(Contact(555));
        await Send(Press("r:" + code));
        await Send(Text("دزدکی"));
        using var db = NewDb();
        Assert.Single(db.Tickets.Include(x => x.Messages).Single().Messages);
    }

    [Fact]
    public async Task A_redelivered_update_is_handled_once()
    {
        Assert.True(await Send(Text("/start"), id: 42));
        Assert.False(await Send(Text("/start"), id: 42));
        Assert.Single(_bot.Calls, c => c.Method == "sendMessage");
    }

    [Fact]
    public async Task Group_messages_are_ignored()
    {
        await Send(new { message_id = 1, chat = new { id = -100, type = "supergroup" }, from = From, text = "/start" });
        using var db = NewDb();
        Assert.Empty(db.MessengerAccounts);
        Assert.Empty(_bot.Calls);
    }

    [Fact]
    public async Task Staff_answer_reaches_the_linked_chat()
    {
        var code = OpenTicket();
        await Send(Contact(555));
        _bot.Calls.Clear();
        using (var db = NewDb())
        {
            var t = db.Tickets.Include(x => x.Messages).Single();
            t.ReplyFromStaff("بله، موجود است.", TestData.Now);
            db.SaveChanges();
        }
        using (var db = NewDb())
        {
            var m = db.OutboxMessages.Single(x => x.Type == nameof(TicketAnswered));
            var ok = await new BotTicketNotifier(db, new BotClients(new Factory(_bot), _cfg), NullLogger<BotTicketNotifier>.Instance)
                .TicketAnsweredAsync(m.AggregateId, m.RefId);
            Assert.True(ok);
        }
        var call = Assert.Single(_bot.Calls);
        Assert.Contains("chat_id", call.Body);
        Assert.Contains(Me, call.Body);
        Assert.Contains("بله، موجود است.", call.Body);
        Assert.Contains("r:" + code, call.Body);
    }
}
