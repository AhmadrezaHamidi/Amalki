using Amlak.Application.Consultants;
using Amlak.Application.Leads;
using Amlak.Domain;
using Amlak.Web.Data;
using Amlak.Web.Read;
using Amlak.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Amlak.Tests;

public sealed class ConsultantTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly ManualClock _clock = new(TestData.Now);

    public ConsultantTests() => _conn.Open();

    public void Dispose() => _conn.Dispose();

    private AppDbContext NewDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options);
        db.Database.EnsureCreated();
        return db;
    }

    private static ConsultantProfile Profile(string name = "سارا محمدی", string? cities = "تهران، لواسان", int years = 5) =>
        new(name, "مشاور فروش", PhoneNumber.Parse("09121112233"), cities, years);

    [Fact]
    public void Consultant_keeps_its_rules()
    {
        Assert.Throws<DomainException>(() => Consultant.Create(Profile(name: " "), TestData.Now));
        Assert.Throws<DomainException>(() => Consultant.Create(Profile(years: 80), TestData.Now));
        Assert.Throws<DomainException>(() => Consultant.Create(Profile() with { PhotoUrl = "javascript:alert(1)" }, TestData.Now));

        var c = Consultant.Create(Profile(cities: "تهران , لواسان،تهران"), TestData.Now);
        Assert.Equal("تهران، لواسان", c.Cities);
        Assert.True(c.Covers("تهران"));
        Assert.False(c.Covers("مشهد"));
        Assert.True(Consultant.Create(Profile(cities: ""), TestData.Now).Covers("مشهد"));
    }

    [Fact]
    public void Listing_page_picks_the_city_consultant_then_the_one_for_every_city()
    {
        var everywhere = Consultant.Create(Profile("کارشناس", cities: null), TestData.Now);
        var tehran = Consultant.Create(Profile("تهرانی", cities: "تهران"), TestData.Now);
        var team = new[] { everywhere, tehran };

        Assert.Same(tehran, ConsultantQueries.Pick(team, "تهران"));
        Assert.Same(everywhere, ConsultantQueries.Pick(team, "مشهد"));
        Assert.Null(ConsultantQueries.Pick([tehran], "مشهد"));
    }

    [Fact]
    public async Task Staff_add_edit_and_hide_consultants_and_leads_keep_the_consultant()
    {
        await using var db = NewDb();
        var commands = new ConsultantCommands(new EfConsultantRepository(db), db, _clock);
        Assert.NotNull((await commands.CreateAsync(Profile(name: ""))).Error);
        var id = (await commands.CreateAsync(Profile())).Value;
        Assert.True((await commands.UpdateAsync(id, Profile(name: "سارا کریمی"))).Succeeded);
        Assert.True((await commands.UpdateAsync(999, Profile())).IsNotFound);

        var leads = new LeadCommands(new EfLeadRepository(db), db, _clock);
        await leads.SubmitAsync("مهمان", PhoneNumber.Parse("09351234567"), "موضوع: خرید ملک", null, id);

        var queries = new ConsultantQueries(db);
        Assert.Equal("سارا کریمی", (await queries.ForCityAsync("لواسان"))!.Name);

        Assert.True((await commands.ToggleActiveAsync(id)).Succeeded);
        Assert.Empty(await queries.ActiveAsync());
        Assert.Equal(id, (await db.Leads.SingleAsync()).ConsultantId);
    }

    [Fact]
    public void Sample_listings_are_realistic_and_valid()
    {
        var items = SampleListings.Make(DevSeed.SampleCount, TestData.Now);
        Assert.Equal(DevSeed.SampleCount, items.Count);
        Assert.All(items, p => Assert.Equal(ListingStatus.Published, p.Status));
        Assert.Equal(items.Count, items.Select(p => p.Slug).Distinct().Count());
        Assert.True(items.Select(p => p.City).Distinct().Count() >= 10);
        Assert.Contains(items, p => p.Deal == DealType.Rent && p.MonthlyRent > 0);
        Assert.Contains(items, p => p.Deal == DealType.ShortTerm);
        // Tehran apartments for sale: tens of millions to a few hundred million Toman per m².
        Assert.All(items.Where(p => p.Deal == DealType.Sale && p.Kind == PropertyKind.Apartment && p.City == "تهران"),
            p => Assert.InRange(p.Price!.Value / p.AreaM2, 30_000_000, 450_000_000));
    }

    [Fact]
    public void Plan_prices_can_be_changed_from_settings()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Plans:featured:Price"] = "250000" }).Build();
        Assert.Equal(250_000, Plans.Find(cfg, "featured")!.Price);
        Assert.Equal(0, Plans.Find(cfg, "free")!.Price);
        Assert.Null(Plans.Find(cfg, "nope"));
    }
}
