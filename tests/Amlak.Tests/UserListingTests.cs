using Amlak.Web.Data;
using Amlak.Web.Domain;
using Amlak.Web.Services;
using Amlak.Web.Services.Publishing;
using Amlak.Web.Services.Sms;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Tests;

public class UserListingTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero));
    private readonly CapturingSms _sms = new();
    private readonly IDataProtectionProvider _dp = new EphemeralDataProtectionProvider();

    public UserListingTests() => _conn.Open();
    public void Dispose() => _conn.Dispose();

    private AppDbContext NewDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options);
        db.Database.EnsureCreated();
        return db;
    }

    private PhoneVerification NewVerification(AppDbContext db) => new(db, _sms, _dp, _clock);

    [Fact]
    public async Task Code_is_sent_once_per_window_and_verifies_once()
    {
        using var db = NewDb();
        var v = NewVerification(db);

        Assert.Equal(SendResult.Sent, await v.SendAsync("09121234567"));
        Assert.Equal(SendResult.TooSoon, await v.SendAsync("09121234567"));
        Assert.Single(_sms.Sent);

        Assert.False(await v.VerifyAsync("09121234567", "000000" == _sms.Sent[0].Code ? "111111" : "000000"));
        Assert.True(await v.VerifyAsync("09121234567", Fa.Digits(_sms.Sent[0].Code))); // Persian digits accepted
        Assert.False(await v.VerifyAsync("09121234567", _sms.Sent[0].Code)); // single use
    }

    [Fact]
    public async Task Code_expires_and_locks_after_too_many_wrong_tries()
    {
        using var db = NewDb();
        var v = NewVerification(db);

        await v.SendAsync("09120000001");
        _clock.Advance(PhoneVerification.CodeLifetime + TimeSpan.FromSeconds(1));
        Assert.False(await v.VerifyAsync("09120000001", _sms.Sent[^1].Code));

        await v.SendAsync("09120000001");
        var code = _sms.Sent[^1].Code;
        var wrong = code == "123456" ? "654321" : "123456";
        for (var i = 0; i < PhoneVerification.MaxAttempts; i++) Assert.False(await v.VerifyAsync("09120000001", wrong));
        Assert.False(await v.VerifyAsync("09120000001", code)); // locked even with the right code
    }

    [Fact]
    public void Verified_phone_cookie_round_trips_and_expires()
    {
        using var db = NewDb();
        var v = NewVerification(db);
        var response = new DefaultHttpContext().Response;
        v.SignIn(response, "09121234567");
        var cookie = response.Headers.SetCookie.ToString().Split(';')[0].Split('=', 2)[1];

        var request = new DefaultHttpContext().Request;
        request.Headers.Cookie = $"{PhoneVerification.CookieName}={cookie}";
        Assert.Equal("09121234567", v.CurrentPhone(request));

        var forged = new DefaultHttpContext().Request;
        forged.Headers.Cookie = $"{PhoneVerification.CookieName}=x{cookie}";
        Assert.Null(v.CurrentPhone(forged));

        _clock.Advance(PhoneVerification.Session + TimeSpan.FromMinutes(1));
        Assert.Null(v.CurrentPhone(request));
    }

    [Fact]
    public void Pending_listing_alert_names_owner_and_admin_link()
    {
        var p = new Property { Id = 7, Title = "آپارتمان", Deal = DealType.Rent, Kind = PropertyKind.Apartment, City = "تهران", District = "ونک", OwnerName = "سارا", OwnerPhone = "09121234567" };
        var text = CaptionBuilder.Submitted(p, "https://example.ir/");
        Assert.Contains("در انتظار بررسی", text);
        Assert.Contains("09121234567", text);
        Assert.Contains("https://example.ir/Admin/Edit/7", text);
    }

    private sealed class CapturingSms : ISmsSender
    {
        public List<(string Mobile, string Code)> Sent { get; } = [];
        public Task<bool> SendCodeAsync(string mobile, string code, CancellationToken ct = default)
        {
            Sent.Add((mobile, code));
            return Task.FromResult(true);
        }
    }

    private sealed class FakeClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }
}
