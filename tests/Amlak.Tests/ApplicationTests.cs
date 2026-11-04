using System.Reflection;
using Amlak.Application;
using Amlak.Application.Leads;
using Amlak.Application.Listings;
using Amlak.Application.Support;
using Amlak.Domain;
using Amlak.Web.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Tests;

/// <summary>The dependency rule of the layers: Domain knows nothing, Application knows only the Domain.</summary>
public class ArchitectureTests
{
    private static readonly string[] Frameworks = ["Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Minio", "SkiaSharp", "Amlak.Web", "Amlak.Infrastructure"];

    private static IEnumerable<string> References(Assembly a) => a.GetReferencedAssemblies().Select(x => x.Name!);

    [Fact]
    public void Domain_references_no_framework_and_no_other_layer()
    {
        var refs = References(typeof(Property).Assembly).ToList();
        Assert.DoesNotContain(refs, r => Frameworks.Any(r.StartsWith) || r.StartsWith("Amlak.Application"));
    }

    [Fact]
    public void Application_references_only_the_domain()
    {
        var refs = References(typeof(ListingCommands).Assembly).ToList();
        Assert.Contains("Amlak.Domain", refs);
        Assert.DoesNotContain(refs, r => Frameworks.Any(r.StartsWith));
    }

    [Fact]
    public void Infrastructure_implements_ports_without_knowing_the_web_or_ef()
    {
        var refs = References(typeof(Amlak.Infrastructure.DependencyInjection).Assembly).ToList();
        Assert.Contains("Amlak.Application", refs);
        Assert.DoesNotContain(refs, r => r.StartsWith("Amlak.Web") || r.StartsWith("Microsoft.EntityFrameworkCore") || r.StartsWith("Microsoft.AspNetCore"));
    }

    [Fact]
    public void Aggregate_state_cannot_be_set_from_outside_the_domain()
    {
        foreach (var type in new[] { typeof(Property), typeof(PropertyPhoto), typeof(Lead), typeof(Ticket), typeof(TicketMessage), typeof(PhoneCode) })
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.SetMethod is not null))
            Assert.False(prop.SetMethod!.IsPublic && prop.Name != nameof(Property.ChannelPosts), $"{type.Name}.{prop.Name} has a public setter");
    }
}

/// <summary>Use cases against the real EF adapters (SQLite): they enforce rules, commit, and write the outbox.</summary>
public sealed class UseCaseTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly ManualClock _clock = new(TestData.Now);

    public UseCaseTests() => _conn.Open();
    public void Dispose() => _conn.Dispose();

    private AppDbContext NewDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options);
        db.Database.EnsureCreated();
        return db;
    }

    private ListingCommands Listings(AppDbContext db) => new(new EfListingRepository(db), db, _clock);

    private static ListingForm Form(string title = "آپارتمان نوساز", long? price = 9_000_000_000, DealType deal = DealType.Sale) => new()
    {
        Title = title, Description = "توضیح", Deal = deal, Kind = PropertyKind.Apartment, AreaM2 = 100,
        City = "تهران", District = "ونک", Price = price,
    };

    [Fact]
    public async Task Saving_a_new_published_listing_gives_a_unique_slug_and_writes_outbox_rows()
    {
        await using var db = NewDb();
        var first = await Listings(db).SaveAsync(null, Form(), ListingStatus.Published, PriceDisplay.Full, []);
        var second = await Listings(db).SaveAsync(null, Form(), ListingStatus.Draft, PriceDisplay.Full, []);

        Assert.True(first.Succeeded && second.Succeeded);
        var slugs = await db.Properties.OrderBy(p => p.Id).Select(p => p.Slug).ToListAsync();
        Assert.Equal(["آپارتمان-نوساز", "آپارتمان-نوساز-2"], slugs);
        var types = await db.OutboxMessages.Where(m => m.AggregateId == first.Value).Select(m => m.Type).ToListAsync();
        Assert.Contains(nameof(ListingPublished), types);
    }

    [Fact]
    public async Task A_broken_rule_comes_back_as_a_persian_error_and_nothing_is_saved()
    {
        await using var db = NewDb();
        var r = await Listings(db).SaveAsync(null, Form(price: null), ListingStatus.Published, PriceDisplay.Full, []);

        Assert.False(r.Succeeded);
        Assert.Equal("برای فروش، قیمت لازم است", r.Error);
        Assert.Equal(0, await db.Properties.CountAsync());
    }

    [Fact]
    public async Task Unknown_ids_are_not_found()
    {
        await using var db = NewDb();
        Assert.True((await Listings(db).SaveAsync(42, Form(), ListingStatus.Draft, PriceDisplay.Full, [])).IsNotFound);
        Assert.True((await Listings(db).TogglePublishedAsync(42)).IsNotFound);
        Assert.True((await Listings(db).DeleteAsync(42)).IsNotFound);
        Assert.True((await new LeadCommands(new EfLeadRepository(db), db, _clock).ToggleHandledAsync(42)).IsNotFound);
        Assert.True((await new TicketCommands(new EfTicketRepository(db), db, _clock).ReplyFromStaffAsync(42, "سلام")).IsNotFound);
    }

    [Fact]
    public async Task Visitors_get_a_limited_number_of_pending_submissions()
    {
        await using var db = NewDb();
        for (var i = 0; i < ListingCommands.MaxPendingPerOwner; i++)
            Assert.True((await Listings(db).SubmitAsync("علی", "09121112233", Form($"آگهی {i}"), [])).Succeeded);

        var refused = await Listings(db).SubmitAsync("علی", "09121112233", Form("یکی دیگر"), []);
        Assert.False(refused.Succeeded);
        Assert.All(await db.Properties.ToListAsync(), p => Assert.Equal(ListingStatus.Pending, p.Status));
        Assert.True((await Listings(db).SubmitAsync("مریم", "09350000000", Form("از شمارهٔ دیگر"), [])).Succeeded);
    }

    [Fact]
    public async Task Deleting_a_listing_records_its_files_for_cleanup()
    {
        await using var db = NewDb();
        var id = (await Listings(db).SaveAsync(null, Form(), ListingStatus.Draft, PriceDisplay.Full, [PropertyPhoto.Stored("/u/a.jpg")])).Value;

        await using var db2 = NewDb();
        Assert.True((await Listings(db2).DeleteAsync(id)).Succeeded);
        Assert.Equal(0, await db2.Properties.CountAsync());
        var deleted = await db2.OutboxMessages.SingleAsync(m => m.Type == nameof(ListingDeleted));
        Assert.Contains("/u/a.jpg", deleted.Payload);
    }

    [Fact]
    public async Task Tickets_open_with_a_code_and_take_replies_by_code_until_closed()
    {
        await using var db = NewDb();
        var tickets = new TicketCommands(new EfTicketRepository(db), db, _clock);
        var code = (await tickets.OpenAsync("سارا", PhoneNumber.Parse("09121112233"), "سؤال", TicketCategory.General, "سلام")).Value!;

        Assert.True((await tickets.ReplyFromVisitorAsync(TicketCodes.Pretty(code).ToLowerInvariant(), "دوباره سلام")).Succeeded);
        var id = (await db.Tickets.SingleAsync()).Id;
        Assert.True((await tickets.ChangeStatusAsync(id, TicketStatus.Closed)).Succeeded);
        var refused = await tickets.ReplyFromVisitorAsync(code, "هنوز هستید؟");
        Assert.False(refused.Succeeded);
        Assert.NotNull(refused.Error);

        await using var check = NewDb();
        Assert.Equal(2, await check.TicketMessages.CountAsync());
    }

    [Fact]
    public async Task Leads_are_submitted_and_toggled()
    {
        await using var db = NewDb();
        var leads = new LeadCommands(new EfLeadRepository(db), db, _clock);
        Assert.False((await leads.SubmitAsync("", PhoneNumber.Parse("09121112233"), null, null)).Succeeded);
        var id = (await leads.SubmitAsync("سارا", PhoneNumber.Parse("09121112233"), "بازدید", null)).Value;

        Assert.True((await leads.ToggleHandledAsync(id)).Succeeded);
        Assert.True((await db.Leads.SingleAsync()).Handled);
        Assert.Contains(await db.OutboxMessages.Select(m => m.Type).ToListAsync(), t => t == nameof(LeadSubmitted));
    }
}
