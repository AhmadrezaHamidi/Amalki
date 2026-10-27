using Amlak.Web.Data;
using Amlak.Web.Domain;
using Amlak.Web.Services;
using Amlak.Web.Services.Publishing;
using Amlak.Web.Services.Sms;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Amlak.Tests;

/// <summary>Records texts instead of sending them; <see cref="Fail"/> makes the provider refuse.</summary>
internal sealed class RecordingSms : ISmsSender
{
    public List<(string Mobile, string Text)> Texts { get; } = [];
    public bool Fail { get; set; }

    public Task<bool> SendCodeAsync(string mobile, string code, CancellationToken ct = default) => Task.FromResult(true);

    public Task<bool> SendTextAsync(string mobile, string text, CancellationToken ct = default)
    {
        if (!Fail) Texts.Add((mobile, text));
        return Task.FromResult(!Fail);
    }
}

public sealed class SmsTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly ManualClock _clock = new(TestData.Now);
    private readonly RecordingSms _sms = new();
    private readonly IConfiguration _cfg = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Site:BaseUrl"] = "https://example.ir/" }).Build();

    public SmsTests() => _conn.Open();
    public void Dispose() => _conn.Dispose();

    private AppDbContext NewDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options);
        db.Database.EnsureCreated();
        return db;
    }

    private SmsGateway Gateway(AppDbContext db) => new(db, _sms, _clock);
    private AlertService Alerts(AppDbContext db) => new(db, Gateway(db), _cfg, _clock);

    private static SavedSearch Search(string phone = "09121234567", DealType deal = DealType.Sale, string city = "تهران",
        string districts = "", long? maxBudget = null, int? minBedrooms = null, PropertyKind? kind = null) => new()
    {
        Phone = phone, Deal = deal, City = city, Districts = districts, MaxBudget = maxBudget, MinBedrooms = minBedrooms, Kind = kind,
        CreatedAtUtc = TestData.Now.AddDays(-1),
    };

    private static Property Rent(long? deposit, long? rent)
    {
        var p = Property.Create("r", ListingDetails.Of("اجاره", "d", PropertyKind.Apartment, 80, "تهران", "ونک", bedrooms: 2),
            Pricing.ForRent(deposit, rent), TestData.Now);
        p.Publish(TestData.Now);
        return p;
    }

    [Fact]
    public void Matcher_checks_place_kind_rooms_and_budget()
    {
        var p = TestData.Listing(district: "سعادت‌آباد", price: 10_000_000_000);
        Assert.True(AlertMatcher.Matches(Search(districts: "ونک، سعادت آباد"), p)); // with or without the half-space
        Assert.True(AlertMatcher.Matches(Search(city: "تهران "), p));
        Assert.False(AlertMatcher.Matches(Search(city: "کرج"), p));
        Assert.False(AlertMatcher.Matches(Search(districts: "ونک"), p));
        Assert.False(AlertMatcher.Matches(Search(deal: DealType.Rent), p));
        Assert.False(AlertMatcher.Matches(Search(kind: PropertyKind.Villa), p));
        Assert.False(AlertMatcher.Matches(Search(maxBudget: 9_000_000_000), p));
        Assert.False(AlertMatcher.Matches(Search(minBedrooms: 1), p)); // the test listing states no bedrooms

        // A rent is compared by its full deposit: 200M + 10M a month ≈ 533M.
        var rent = Rent(200_000_000, 10_000_000);
        Assert.Equal(200_000_000 + 333_333_333, PriceText.Comparable(rent));
        Assert.True(AlertMatcher.Matches(Search(deal: DealType.Rent, maxBudget: 600_000_000, minBedrooms: 2), rent));
        Assert.False(AlertMatcher.Matches(Search(deal: DealType.Rent, maxBudget: 500_000_000), rent));
    }

    [Fact]
    public async Task New_listing_then_price_drops_are_each_texted_once()
    {
        using var db = NewDb();
        var s = Search();
        db.SavedSearches.Add(s);
        var p = TestData.Listing(price: 10_000_000_000);
        db.Properties.Add(p);
        db.SaveChanges();
        var alerts = Alerts(db);

        Assert.Equal(1, await alerts.NotifyAsync(p.Id));
        Assert.Equal(0, await alerts.NotifyAsync(p.Id)); // the outbox may run a handler twice
        Assert.Contains("آگهی تازه", _sms.Texts[0].Text);
        Assert.EndsWith(SmsText.OptOutFooter, _sms.Texts[0].Text);

        p.Update(ListingDetails.Of(p.Title, "d", p.Kind, p.AreaM2, p.City, p.District), Pricing.ForSale(9_000_000_000), TestData.Now);
        db.SaveChanges();
        Assert.Equal(1, await alerts.NotifyAsync(p.Id));
        Assert.Contains("کاهش قیمت", _sms.Texts[1].Text);
        Assert.Contains("۱۰ میلیارد", _sms.Texts[1].Text);
        Assert.Contains("۹ میلیارد", _sms.Texts[1].Text);

        // Back up, then down to a price above the lowest one told: nothing new to say.
        p.Update(ListingDetails.Of(p.Title, "d", p.Kind, p.AreaM2, p.City, p.District), Pricing.ForSale(9_500_000_000), TestData.Now);
        db.SaveChanges();
        Assert.Equal(0, await alerts.NotifyAsync(p.Id));

        // A hidden price is never revealed by a drop text.
        p.Update(ListingDetails.Of(p.Title, "d", p.Kind, p.AreaM2, p.City, p.District), Pricing.ForSale(8_000_000_000), TestData.Now);
        p.ShowPriceAs(PriceDisplay.Negotiable, TestData.Now);
        db.SaveChanges();
        Assert.Equal(0, await alerts.NotifyAsync(p.Id));
        Assert.Equal(2, _sms.Texts.Count);
    }

    [Fact]
    public async Task Old_listings_drafts_and_failed_sends_are_not_recorded_as_told()
    {
        using var db = NewDb();
        var late = Search();
        late.CreatedAtUtc = TestData.Now.AddDays(1); // signed up after the listing went live
        db.SavedSearches.Add(late);
        var live = TestData.Listing(slug: "a");
        var draft = TestData.Listing(slug: "b", published: false);
        db.Properties.AddRange(live, draft);
        db.SaveChanges();

        Assert.Equal(0, await Alerts(db).NotifyAsync(live.Id));
        Assert.Equal(0, await Alerts(db).NotifyAsync(draft.Id));

        late.CreatedAtUtc = TestData.Now.AddDays(-1);
        db.SaveChanges();
        _sms.Fail = true;
        Assert.Equal(0, await Alerts(db).NotifyAsync(live.Id));
        Assert.Empty(db.AlertsSent);
        Assert.False(db.SmsLogs.Single().Ok);
        _sms.Fail = false;
        Assert.Equal(1, await Alerts(db).NotifyAsync(live.Id)); // tried again on the next change
    }

    [Fact]
    public async Task Opting_out_stops_alerts_and_campaigns_until_a_new_sign_up()
    {
        using var db = NewDb();
        db.SavedSearches.Add(Search());
        db.SaveChanges();
        var gateway = Gateway(db);

        await gateway.OptOutAsync("09121234567");
        Assert.False(db.SavedSearches.Single().Active);
        Assert.False(await gateway.SendAsync("09121234567", "سلام"));
        Assert.Empty(_sms.Texts);

        await gateway.OptInAsync("09121234567");
        db.SaveChanges();
        Assert.True(await gateway.SendAsync("+98 912 123 4567", "سلام"));
        Assert.Equal(("09121234567", "سلام\n" + SmsText.OptOutFooter), _sms.Texts.Single());
    }

    [Fact]
    public async Task Campaign_goes_to_distinct_valid_numbers_when_due()
    {
        using var db = NewDb();
        db.SmsOptOuts.Add(new SmsOptOut { Phone = "09120000003" });
        var p = TestData.Listing(price: 12_500_000_000);
        db.Properties.Add(p);
        db.SaveChanges();
        var campaign = new SmsCampaign
        {
            Name = "تست", Template = "{نام} عزیز، {عنوان} — {قیمت}\n{لینک}", Audience = CampaignAudience.Uploaded,
            UploadedPhones = "09120000001\n۰۹۱۲۰۰۰۰۰۰۱\nنامعتبر\n09120000002, 09120000003", PropertyId = p.Id,
            ScheduledAtUtc = TestData.Now.AddHours(1),
        };
        db.SmsCampaigns.Add(campaign);
        db.SaveChanges();
        var bulk = new BulkSmsService(db, Gateway(db), _cfg, _clock);

        Assert.Equal(["09120000001", "09120000002"], (await bulk.RecipientsAsync(campaign)).Select(r => r.Phone));
        Assert.Equal(0, await bulk.ProcessDueAsync()); // not yet

        _clock.UtcNow = TestData.Now.AddHours(2);
        Assert.Equal(1, await bulk.ProcessDueAsync());
        Assert.Equal(0, await bulk.ProcessDueAsync());
        Assert.Equal((CampaignStatus.Done, 2, 2, 0), (campaign.Status, campaign.Recipients, campaign.Sent, campaign.Failed));
        var text = _sms.Texts[0].Text;
        Assert.StartsWith("کاربر عزیز، آپارتمان تست — ۱۲٫۵ میلیارد تومان", text);
        Assert.Contains($"https://example.ir/p/{p.Id}", text);
        Assert.Equal(1, text.Split(SmsText.OptOutFooter).Length - 1);
        Assert.Equal(2, db.SmsLogs.Count(l => l.CampaignId == campaign.Id));
    }

    [Fact]
    public void Price_display_controls_what_visitors_and_channels_see()
    {
        var p = TestData.Listing(area: 100, price: 10_000_000_000);
        Assert.Equal("۱۰ میلیارد تومان · متری ۱۰۰ میلیون", PriceText.Label(p));

        p.ShowPriceAs(PriceDisplay.TotalOnly, TestData.Now);
        Assert.Equal("۱۰ میلیارد تومان", PriceText.Label(p));
        Assert.Null(PriceText.PerSquareMeter(p));
        Assert.IsType<ListingChanged>(Assert.Single(p.DomainEvents));

        p.ShowPriceAs(PriceDisplay.Negotiable, TestData.Now);
        Assert.Equal("توافقی", PriceText.Label(p));
        var caption = CaptionBuilder.Build(p, "https://example.ir");
        Assert.Contains("قیمت: توافقی", caption);
        Assert.DoesNotContain("میلیارد", caption);

        p.ClearDomainEvents();
        p.ShowPriceAs(PriceDisplay.Negotiable, TestData.Now);
        Assert.Empty(p.DomainEvents); // no change, no event
        Assert.Throws<DomainException>(() => p.ShowPriceAs((PriceDisplay)9, TestData.Now));
    }

    [Fact]
    public void Text_helpers_fill_placeholders_and_count_parts()
    {
        Assert.Equal("سلام سارا {ناشناخته}", SmsText.Render("سلام {نام} {ناشناخته}", new Dictionary<string, string?> { ["نام"] = "سارا" }));
        Assert.Equal("a\n" + SmsText.OptOutFooter, SmsText.WithOptOut(SmsText.WithOptOut("a")));
        Assert.Equal(1, SmsText.Segments(new string('a', 160)));
        Assert.Equal(2, SmsText.Segments(new string('a', 161)));
        Assert.Equal(1, SmsText.Segments(new string('س', 70)));
        Assert.Equal(2, SmsText.Segments(new string('س', 71)));
        Assert.Equal(1_500_000, Fa.Amount("۱٬۵۰۰٬۰۰۰"));
        Assert.Null(Fa.Amount("بدون عدد"));
        Assert.Equal("۱۴۰۵/۰۷/۲۸ ۰۳:۳۰", Fa.When(TestData.Now)); // 2026-10-20 00:00 UTC in Tehran
    }
}
