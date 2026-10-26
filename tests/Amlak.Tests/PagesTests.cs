using Amlak.Web.Data;
using Amlak.Web.Domain;
using Amlak.Web.Pages.Admin;
using Amlak.Web.Services;
using Amlak.Web.Services.Sms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TrackModel = Amlak.Web.Pages.Track.IndexModel;

namespace Amlak.Tests;

internal sealed class MemoryTempData : ITempDataProvider
{
    private IDictionary<string, object> _data = new Dictionary<string, object>();
    public IDictionary<string, object> LoadTempData(HttpContext context) => _data;
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) => _data = values;
}

public sealed class PagesTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly FakeSmsSender _sender = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 25, 10, 0, 0, TimeSpan.Zero));

    public PagesTests() => _conn.Open();
    public void Dispose() => _conn.Dispose();

    private AppDbContext NewDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options);
        db.Database.EnsureCreated();
        return db;
    }

    private static T WithContext<T>(T page) where T : PageModel
    {
        var http = new DefaultHttpContext();
        page.PageContext = new PageContext(new ActionContext(http, new RouteData(), new CompiledPageActionDescriptor(), new ModelStateDictionary()));
        page.TempData = new TempDataDictionary(http, new MemoryTempData());
        return page;
    }

    private SmsGateway Gateway(AppDbContext db) => new(db, _sender, NullLogger<SmsGateway>.Instance);

    [Theory]
    [InlineData("۱۵٬۰۰۰٬۰۰۰٬۰۰۰", 15_000_000_000L)]
    [InlineData("12,500", 12_500L)]
    [InlineData("٤٠٠", 400L)]
    [InlineData("ده میلیون", null)]
    [InlineData("", null)]
    public void Parses_numbers_typed_in_any_digits(string input, long? expected) =>
        Assert.Equal(expected, Fa.ParseLong(input));

    [Fact]
    public void Formats_tehran_time_in_persian_calendar() =>
        // 2026-10-25 06:30 UTC is 10:00 in Tehran, 3 Aban 1405.
        Assert.Equal("۱۴۰۵/۰۸/۰۳ ۱۰:۰۰", Fa.When(new DateTime(2026, 10, 25, 6, 30, 0, DateTimeKind.Utc)));

    [Fact]
    public async Task Track_page_subscribes_only_after_code_and_clears_old_opt_out()
    {
        using var db = NewDb();
        db.SmsOptOuts.Add(new SmsOptOut { Phone = "09123456789" });
        await db.SaveChangesAsync();
        var otp = new OtpService(db, Gateway(db), _clock);
        var input = new TrackModel.InputModel
        {
            Phone = "۰۹۱۲ ۳۴۵ ۶۷۸۹", City = "تهران", Districts = "سعادت‌آباد,شهرک غرب", MaxBudget = "۱۵٬۰۰۰٬۰۰۰٬۰۰۰", MinBedrooms = 2,
        };

        var page = WithContext(new TrackModel(db, otp) { Input = input });
        await page.OnPostSendAsync();
        Assert.True(page.CodeSent);
        Assert.Empty(db.SavedSearches);

        page = WithContext(new TrackModel(db, otp) { Input = input });
        input.Code = "00000";
        await page.OnPostConfirmAsync();
        Assert.False(page.Done);
        Assert.Empty(db.SavedSearches);

        var code = new string(_sender.Sent[^1].Text.Where(c => c is >= '۰' and <= '۹').ToArray());
        page = WithContext(new TrackModel(db, otp) { Input = input });
        input.Code = code; // Persian digits as typed on a phone keyboard
        await page.OnPostConfirmAsync();

        Assert.True(page.Done);
        var s = await db.SavedSearches.SingleAsync();
        Assert.Equal(("09123456789", 15_000_000_000L, "سعادت‌آباد، شهرک غرب"), (s.Phone, s.MaxBudget!.Value, s.Districts));
        Assert.False(await db.SmsOptOuts.AnyAsync());
    }

    [Fact]
    public async Task Admin_sms_schedules_in_tehran_time_and_rejects_empty_upload()
    {
        using var db = NewDb();
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Site:BaseUrl"] = "https://example.ir" }).Build();
        var bulk = new BulkSmsService(db, Gateway(db), cfg, _clock);

        var page = WithContext(new SmsModel(db, bulk, _clock));
        page.Input = new SmsModel.InputModel { Name = "x", Audience = CampaignAudience.Uploaded, UploadedPhones = " " };
        Assert.IsType<PageResult>(await page.OnPostCreateAsync());
        Assert.Empty(db.SmsCampaigns);

        page = WithContext(new SmsModel(db, bulk, _clock));
        page.Input = new SmsModel.InputModel
        {
            Name = "معرفی", Audience = CampaignAudience.Uploaded, UploadedPhones = "09121111111\n09121111111",
            ScheduleLocal = "2026-10-25T18:00",
        };
        await page.OnPostPreviewAsync();
        Assert.Equal(1, page.Shown!.Recipients);
        Assert.EndsWith(SmsText.OptOutFooter, page.Shown.Sample);

        Assert.IsType<RedirectToPageResult>(await page.OnPostCreateAsync());
        var c = await db.SmsCampaigns.SingleAsync();
        Assert.Equal(new DateTime(2026, 10, 25, 14, 30, 0), c.ScheduledAtUtc); // 18:00 Tehran = 14:30 UTC
        Assert.Equal(CampaignStatus.Scheduled, c.Status);

        await page.OnPostCancelAsync(c.Id);
        Assert.Equal(CampaignStatus.Cancelled, (await db.SmsCampaigns.AsNoTracking().SingleAsync()).Status);
    }
}
