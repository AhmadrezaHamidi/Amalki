using Amlak.Web.Data;
using Amlak.Web.Domain;
using Amlak.Web.Services;
using Amlak.Web.Services.Publishing;
using Amlak.Web.Services.Sms;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Amlak.Tests;

internal sealed class FakeSmsSender : ISmsSender
{
    public List<(string Phone, string Text)> Sent { get; } = [];
    public Task<SmsResult> SendAsync(string phone, string text, CancellationToken ct = default)
    {
        Sent.Add((phone, text));
        return Task.FromResult(new SmsResult(true, "m" + Sent.Count));
    }
}

internal sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;
    public override DateTimeOffset GetUtcNow() => Now;
}

public sealed class SmsTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly FakeSmsSender _sender = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 25, 10, 0, 0, TimeSpan.Zero));
    private readonly IConfiguration _cfg = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Site:BaseUrl"] = "https://example.ir" }).Build();

    public SmsTests() => _conn.Open();
    public void Dispose() => _conn.Dispose();

    private AppDbContext NewDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options);
        db.Database.EnsureCreated();
        return db;
    }

    private SmsGateway Gateway(AppDbContext db) => new(db, _sender, NullLogger<SmsGateway>.Instance);

    private static Property Apartment(long price = 12_500_000_000) => new()
    {
        Title = "آپارتمان ۱۲۰ متری", Slug = "apt-120", Description = "d", Deal = DealType.Sale, Kind = PropertyKind.Apartment,
        City = "تهران", District = "سعادت‌آباد", AreaM2 = 120, Bedrooms = 3, Price = price, Status = ListingStatus.Published,
    };

    private static SavedSearch Search(string phone = "09121111111") => new()
    {
        Phone = phone, Deal = DealType.Sale, Kind = PropertyKind.Apartment, City = "تهران",
        Districts = "سعادت‌آباد، شهرک غرب", MaxBudget = 15_000_000_000, MinBedrooms = 2,
    };

    [Theory]
    [InlineData("۰۹۱۲ ۳۴۵ ۶۷۸۹", "09123456789")]
    [InlineData("+98 912-345-6789", "09123456789")]
    [InlineData("00989123456789", "09123456789")]
    [InlineData("9123456789", "09123456789")]
    [InlineData("02112345678", null)]
    [InlineData("0912", null)]
    [InlineData("", null)]
    public void Normalizes_iranian_mobile_numbers(string input, string? expected) =>
        Assert.Equal(expected, PhoneNumber.Normalize(input));

    [Fact]
    public void Counts_unicode_and_gsm_segments()
    {
        Assert.Equal(1, SmsText.Segments(new string('ب', 70)));
        Assert.Equal(2, SmsText.Segments(new string('ب', 71)));
        Assert.Equal(2, SmsText.Segments(new string('ب', 134)));
        Assert.Equal(3, SmsText.Segments(new string('ب', 135)));
        Assert.Equal(1, SmsText.Segments(new string('a', 160)));
        Assert.Equal(2, SmsText.Segments(new string('a', 161)));
    }

    [Fact]
    public void Pricing_per_meter_full_deposit_and_labels()
    {
        var p = Apartment();
        Assert.Equal(104_166_666, Pricing.PerSquareMeter(p));
        Assert.Contains("متری", Pricing.Label(p));

        p.PriceDisplay = PriceDisplay.TotalOnly;
        Assert.DoesNotContain("متری", Pricing.Label(p));

        p.PriceDisplay = PriceDisplay.Negotiable;
        Assert.Equal("توافقی", Pricing.Label(p));
        Assert.Contains("توافقی", CaptionBuilder.Build(p, "https://example.ir"));
        Assert.DoesNotContain("میلیارد", CaptionBuilder.Build(p, "https://example.ir"));

        Assert.Equal(666_666_667, Pricing.FullDeposit(400_000_000, 8_000_000));
    }

    [Fact]
    public void Matcher_respects_districts_budget_kind_and_spelling_variants()
    {
        var p = Apartment();
        var s = Search();
        Assert.True(AlertMatcher.Matches(s, p));

        p.District = "سعادت آباد"; // space instead of ZWNJ
        Assert.True(AlertMatcher.Matches(s, p));

        Assert.False(AlertMatcher.Matches(s, Apartment(price: 16_000_000_000)));
        Assert.False(AlertMatcher.Matches(new SavedSearch { Phone = s.Phone, Deal = DealType.Sale, Kind = PropertyKind.Villa, City = "تهران" }, p));
        Assert.False(AlertMatcher.Matches(new SavedSearch { Phone = s.Phone, Deal = DealType.Sale, City = "تهران", Districts = "ونک" }, p));

        var rent = new Property { Deal = DealType.Rent, City = "تهران", District = "ونک", Deposit = 400_000_000, MonthlyRent = 8_000_000 };
        var rentSearch = new SavedSearch { Phone = s.Phone, Deal = DealType.Rent, City = "تهران", MaxBudget = 600_000_000 };
        Assert.False(AlertMatcher.Matches(rentSearch, rent)); // full-deposit equivalent is ~667M
        rentSearch.MaxBudget = 700_000_000;
        Assert.True(AlertMatcher.Matches(rentSearch, rent));
    }

    [Fact]
    public async Task Alerts_new_listing_once_then_price_drop_and_skip_opted_out()
    {
        using var db = NewDb();
        var p = Apartment();
        db.Properties.Add(p);
        db.SavedSearches.AddRange(Search("09121111111"), Search("09122222222"));
        db.SmsOptOuts.Add(new SmsOptOut { Phone = "09122222222" });
        await db.SaveChangesAsync();
        var alerts = new AlertService(db, Gateway(db), _cfg);

        Assert.Equal(1, await alerts.NotifyAsync(p.Id, previousPrice: null));
        var (phone, text) = Assert.Single(_sender.Sent);
        Assert.Equal("09121111111", phone);
        Assert.Contains("آگهی تازه", text);
        Assert.EndsWith(SmsText.OptOutFooter, text);

        Assert.Equal(0, await alerts.NotifyAsync(p.Id, previousPrice: null)); // saved again, nothing new

        var before = p.Price;
        p.Price = 11_000_000_000;
        await db.SaveChangesAsync();
        Assert.Equal(1, await alerts.NotifyAsync(p.Id, before));
        Assert.Contains("کاهش قیمت", _sender.Sent[^1].Text);

        Assert.Equal(0, await alerts.NotifyAsync(p.Id, before)); // same drop is not sent twice
        Assert.Equal(2, _sender.Sent.Count);
    }

    [Fact]
    public async Task Negotiable_price_drop_is_never_texted()
    {
        using var db = NewDb();
        var p = Apartment();
        db.Properties.Add(p);
        db.SavedSearches.Add(Search());
        await db.SaveChangesAsync();
        var alerts = new AlertService(db, Gateway(db), _cfg);
        await alerts.NotifyAsync(p.Id, null);

        p.PriceDisplay = PriceDisplay.Negotiable;
        p.Price = 10_000_000_000;
        await db.SaveChangesAsync();
        Assert.Equal(0, await alerts.NotifyAsync(p.Id, 12_500_000_000));
    }

    [Fact]
    public async Task Opt_out_deactivates_saved_searches()
    {
        using var db = NewDb();
        db.SavedSearches.Add(Search());
        await db.SaveChangesAsync();
        await Gateway(db).OptOutAsync("+989121111111");
        Assert.False((await db.SavedSearches.AsNoTracking().SingleAsync()).Active);
        Assert.True(await db.SmsOptOuts.AnyAsync(x => x.Phone == "09121111111"));
    }

    private static string CodeFrom(string text) =>
        new(text.Where(c => c is >= '۰' and <= '۹').Select(c => (char)('0' + (c - '۰'))).ToArray());

    [Fact]
    public async Task Otp_verifies_once_limits_resend_and_guesses()
    {
        using var db = NewDb();
        var otp = new OtpService(db, Gateway(db), _clock);

        Assert.Equal(OtpRequestStatus.Sent, (await otp.RequestAsync("09123456789")).Status);
        var code = CodeFrom(_sender.Sent[^1].Text);
        Assert.Equal(5, code.Length);
        Assert.DoesNotContain(code, (await db.PhoneOtps.SingleAsync()).CodeHash); // only a hash is stored

        var again = await otp.RequestAsync("09123456789");
        Assert.Equal(OtpRequestStatus.TooSoon, again.Status);
        Assert.InRange(again.RetryAfterSeconds, 1, 60);

        Assert.True(await otp.VerifyAsync("۰۹۱۲۳۴۵۶۷۸۹", Fa.Digits(code))); // Persian digits accepted
        Assert.False(await otp.VerifyAsync("09123456789", code)); // single use

        _clock.Now = _clock.Now.AddMinutes(2);
        await otp.RequestAsync("09123456789");
        var fresh = CodeFrom(_sender.Sent[^1].Text);
        var wrong = fresh == "11111" ? "22222" : "11111";
        for (var i = 0; i < OtpService.MaxAttempts; i++) Assert.False(await otp.VerifyAsync("09123456789", wrong));
        Assert.False(await otp.VerifyAsync("09123456789", fresh)); // locked after too many guesses

        _clock.Now = _clock.Now.AddMinutes(2);
        await otp.RequestAsync("09123456789");
        var late = CodeFrom(_sender.Sent[^1].Text);
        _clock.Now = _clock.Now.Add(OtpService.Lifetime);
        Assert.False(await otp.VerifyAsync("09123456789", late)); // expired
    }

    [Fact]
    public async Task Campaign_sends_when_due_to_deduped_valid_numbers()
    {
        using var db = NewDb();
        var p = Apartment();
        db.Properties.Add(p);
        db.SmsOptOuts.Add(new SmsOptOut { Phone = "09123333333" });
        await db.SaveChangesAsync();
        var campaign = new SmsCampaign
        {
            Name = "معرفی", Template = "{نام} عزیز، {عنوان} در {محله} — {قیمت}\n{لینک}", Audience = CampaignAudience.Uploaded,
            UploadedPhones = "09121111111\n۰۹۱۲۱۱۱۱۱۱۱\nnot-a-number\n+989122222222\n09123333333",
            PropertyId = p.Id, ScheduledAtUtc = _clock.Now.UtcDateTime.AddHours(1),
        };
        db.SmsCampaigns.Add(campaign);
        await db.SaveChangesAsync();
        var bulk = new BulkSmsService(db, Gateway(db), _cfg, _clock);

        Assert.Equal(2, (await bulk.RecipientsAsync(campaign)).Count);
        Assert.Equal(0, await bulk.ProcessDueAsync());
        Assert.Empty(_sender.Sent);

        _clock.Now = _clock.Now.AddHours(1);
        Assert.Equal(1, await bulk.ProcessDueAsync());
        Assert.Equal(2, _sender.Sent.Count);
        var text = _sender.Sent[0].Text;
        Assert.StartsWith("کاربر عزیز، آپارتمان ۱۲۰ متری در سعادت‌آباد", text);
        Assert.Contains("https://example.ir/", text);
        Assert.EndsWith(SmsText.OptOutFooter, text);

        var done = await db.SmsCampaigns.AsNoTracking().SingleAsync();
        Assert.Equal((CampaignStatus.Done, 2, 2, 0), (done.Status, done.Recipients, done.Sent, done.Failed));
        Assert.Equal(2, await db.SmsLogs.CountAsync(x => x.CampaignId == done.Id));
        Assert.Equal(0, await bulk.ProcessDueAsync()); // not sent twice
    }
}
